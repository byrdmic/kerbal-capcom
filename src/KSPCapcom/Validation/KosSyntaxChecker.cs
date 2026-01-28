using System;
using System.Collections.Generic;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Lightweight heuristic checker for common LLM mistakes in kOS scripts.
    /// Checks for syntax issues like unbalanced braces, markdown artifacts, and smart quotes.
    /// </summary>
    public class KosSyntaxChecker
    {
        /// <summary>
        /// Smart/curly quote characters that should be replaced with straight quotes.
        /// </summary>
        private static readonly char[] SmartQuotes = { '\u201C', '\u201D', '\u2018', '\u2019' };

        /// <summary>
        /// Check a kOS script for syntax issues.
        /// </summary>
        /// <param name="script">The kOS script text to check.</param>
        /// <returns>Result containing any issues found.</returns>
        public KosSyntaxResult Check(string script)
        {
            var result = new KosSyntaxResult();

            if (string.IsNullOrEmpty(script))
            {
                return result;
            }

            var lines = script.Split(new[] { '\n' }, StringSplitOptions.None);

            // Track brace/paren balance
            int braceDepth = 0;
            int parenDepth = 0;
            int braceOpenLine = 0;
            int parenOpenLine = 0;

            // State tracking for comments and strings
            bool inString = false;
            bool inBlockComment = false;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                int lineNumber = lineIndex + 1;
                string line = lines[lineIndex];

                // Check for markdown artifacts (outside of strings/comments)
                CheckMarkdownArtifacts(line, lineNumber, result);

                // Check for smart quotes
                CheckSmartQuotes(line, lineNumber, result);

                // Process character by character for brace/paren matching
                bool inLineComment = false;

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    char next = i + 1 < line.Length ? line[i + 1] : '\0';

                    // Handle block comment state
                    if (inBlockComment)
                    {
                        if (c == '*' && next == '/')
                        {
                            inBlockComment = false;
                            i++; // Skip the /
                        }
                        continue;
                    }

                    // Check for block comment start
                    if (!inString && c == '/' && next == '*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }

                    // Check for line comment start
                    if (!inString && c == '/' && next == '/')
                    {
                        inLineComment = true;
                        break; // Rest of line is comment
                    }

                    // Skip if in line comment
                    if (inLineComment)
                    {
                        break;
                    }

                    // Handle string state
                    if (c == '"' && !inString)
                    {
                        inString = true;
                        continue;
                    }
                    else if (inString)
                    {
                        if (c == '\\' && next != '\0')
                        {
                            i++; // Skip escaped character
                            continue;
                        }
                        if (c == '"')
                        {
                            inString = false;
                        }
                        continue;
                    }

                    // Track braces
                    if (c == '{')
                    {
                        if (braceDepth == 0)
                        {
                            braceOpenLine = lineNumber;
                        }
                        braceDepth++;
                    }
                    else if (c == '}')
                    {
                        braceDepth--;
                        if (braceDepth < 0)
                        {
                            result.AddIssue(SyntaxIssueType.UnbalancedBrace, lineNumber,
                                "Unexpected closing brace `}` - no matching opening brace");
                            braceDepth = 0; // Reset to continue checking
                        }
                    }

                    // Track parentheses
                    if (c == '(')
                    {
                        if (parenDepth == 0)
                        {
                            parenOpenLine = lineNumber;
                        }
                        parenDepth++;
                    }
                    else if (c == ')')
                    {
                        parenDepth--;
                        if (parenDepth < 0)
                        {
                            result.AddIssue(SyntaxIssueType.UnbalancedParenthesis, lineNumber,
                                "Unexpected closing parenthesis `)` - no matching opening parenthesis");
                            parenDepth = 0; // Reset to continue checking
                        }
                    }
                }
            }

            // Check for unclosed braces
            if (braceDepth > 0)
            {
                result.AddIssue(SyntaxIssueType.UnbalancedBrace, braceOpenLine,
                    $"Unclosed brace `{{` - missing {braceDepth} closing brace(s)");
            }

            // Check for unclosed parentheses
            if (parenDepth > 0)
            {
                result.AddIssue(SyntaxIssueType.UnbalancedParenthesis, parenOpenLine,
                    $"Unclosed parenthesis `(` - missing {parenDepth} closing parenthesis(es)");
            }

            // Check for missing terminators on the last non-empty line
            CheckMissingTerminator(lines, result);

            return result;
        }

        /// <summary>
        /// Check a line for markdown artifacts that shouldn't appear in kOS code.
        /// </summary>
        private void CheckMarkdownArtifacts(string line, int lineNumber, KosSyntaxResult result)
        {
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            string trimmed = line.TrimStart();

            // Check for backticks (markdown code markers)
            if (line.Contains("`"))
            {
                // Skip if in a comment
                int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
                int backtickIndex = line.IndexOf('`');

                // If backtick is before comment (or no comment), it's an issue
                if (commentIndex < 0 || backtickIndex < commentIndex)
                {
                    result.AddIssue(SyntaxIssueType.MarkdownBacktick, lineNumber,
                        "Markdown backtick found in code - remove ` characters");
                }
            }

            // Check for markdown bullets at start of line
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                // Make sure it's not a subtraction or multiplication in context
                // A markdown bullet would be at the start of a trimmed line
                result.AddIssue(SyntaxIssueType.MarkdownBullet, lineNumber,
                    "Markdown bullet found - this may be LLM formatting, not valid kOS");
            }

            // Check for markdown headers
            if (trimmed.StartsWith("# ") || trimmed.StartsWith("## ") || trimmed.StartsWith("### "))
            {
                result.AddIssue(SyntaxIssueType.MarkdownHeader, lineNumber,
                    "Markdown header found - this is not valid kOS syntax");
            }
        }

        /// <summary>
        /// Check a line for smart/curly quotes.
        /// </summary>
        private void CheckSmartQuotes(string line, int lineNumber, KosSyntaxResult result)
        {
            foreach (char sq in SmartQuotes)
            {
                if (line.IndexOf(sq) >= 0)
                {
                    result.AddIssue(SyntaxIssueType.SmartQuote, lineNumber,
                        "Smart/curly quote found - use straight quotes \" instead");
                    return; // Only report once per line
                }
            }
        }

        /// <summary>
        /// Check if the script appears to be missing a terminator on the last statement.
        /// kOS requires statements to end with a period.
        /// </summary>
        private void CheckMissingTerminator(string[] lines, KosSyntaxResult result)
        {
            // Find the last non-empty, non-comment line
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];

                // Strip trailing whitespace
                string trimmed = line.TrimEnd();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                // Skip comment-only lines
                string trimmedStart = trimmed.TrimStart();
                if (trimmedStart.StartsWith("//") || trimmedStart.StartsWith("/*"))
                {
                    continue;
                }

                // Remove trailing inline comments
                int commentIndex = FindCommentStart(trimmed);
                if (commentIndex > 0)
                {
                    trimmed = trimmed.Substring(0, commentIndex).TrimEnd();
                }

                // Skip if empty after removing comments
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                // Check if line ends with a valid terminator
                // Valid endings: . (statement), } (block), { (block start)
                char lastChar = trimmed[trimmed.Length - 1];
                if (lastChar != '.' && lastChar != '}' && lastChar != '{')
                {
                    // Could be missing a terminator
                    // Don't flag if it looks like a continuation (ends with operator)
                    if (!EndsWithContinuation(trimmed))
                    {
                        result.AddIssue(SyntaxIssueType.MissingTerminator, i + 1,
                            "Statement may be missing terminator - kOS statements end with `.`");
                    }
                }

                // Only check the last substantive line
                break;
            }
        }

        /// <summary>
        /// Find the start of an inline comment, respecting strings.
        /// </summary>
        private int FindCommentStart(string line)
        {
            bool inString = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (c == '"' && !inString)
                {
                    inString = true;
                }
                else if (inString)
                {
                    if (c == '\\' && next != '\0')
                    {
                        i++; // Skip escaped char
                        continue;
                    }
                    if (c == '"')
                    {
                        inString = false;
                    }
                }
                else if (c == '/' && next == '/')
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Check if a line ends with a continuation pattern (operator, comma, etc.)
        /// </summary>
        private bool EndsWithContinuation(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            // Common continuation patterns
            string[] continuationEndings = { "+", "-", "*", "/", "^", ",", "AND", "OR", "TO", "IS" };

            string upper = line.TrimEnd().ToUpperInvariant();

            foreach (var ending in continuationEndings)
            {
                if (upper.EndsWith(ending))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

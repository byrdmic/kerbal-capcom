using System;
using System.Collections.Generic;
using System.Text;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// State machine tokenizer that extracts kOS identifiers from script text.
    /// Single-pass O(n) implementation that handles comments, strings, and
    /// recognizes user-defined variables.
    /// </summary>
    public class KosIdentifierExtractor
    {
        /// <summary>
        /// Built-in lock targets that should not be marked as user-defined.
        /// </summary>
        private static readonly HashSet<string> BuiltInLockTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "STEERING", "THROTTLE", "WHEELSTEERING", "WHEELTHROTTLE"
        };

        /// <summary>
        /// kOS keywords that should be recognized but not treated as identifiers.
        /// </summary>
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SET", "TO", "DECLARE", "LOCAL", "GLOBAL", "IS", "PARAMETER", "FOR", "IN",
            "FUNCTION", "LOCK", "UNLOCK", "IF", "ELSE", "UNTIL", "WHEN", "THEN",
            "RETURN", "BREAK", "PRESERVE", "ON", "TOGGLE", "WAIT", "PRINT",
            "LOG", "COPY", "RENAME", "DELETE", "SWITCH", "RUN", "RUNPATH",
            "COMPILE", "EDIT", "CLEARSCREEN", "STAGE", "REBOOT", "SHUTDOWN",
            "TRUE", "FALSE", "AND", "OR", "NOT", "FROM", "DO", "CHOOSE"
        };

        private enum State
        {
            Normal,
            InLineComment,
            InBlockComment,
            InString,
            InIdentifier
        }

        /// <summary>
        /// Extract all identifiers from a kOS script.
        /// </summary>
        /// <param name="script">The kOS script text.</param>
        /// <returns>A set of extracted identifiers.</returns>
        public KosIdentifierSet Extract(string script)
        {
            var result = new KosIdentifierSet();

            if (string.IsNullOrEmpty(script))
            {
                return result;
            }

            var state = State.Normal;
            var currentToken = new StringBuilder();
            var tokens = new List<Token>();
            int line = 1;
            int tokenStartLine = 1;

            for (int i = 0; i < script.Length; i++)
            {
                char c = script[i];
                char next = i + 1 < script.Length ? script[i + 1] : '\0';

                // Track line numbers
                if (c == '\n')
                {
                    line++;
                }

                switch (state)
                {
                    case State.Normal:
                        // Check for comment start
                        if (c == '/' && next == '/')
                        {
                            FlushToken(currentToken, tokens, tokenStartLine);
                            state = State.InLineComment;
                            i++; // Skip next char
                        }
                        else if (c == '/' && next == '*')
                        {
                            FlushToken(currentToken, tokens, tokenStartLine);
                            state = State.InBlockComment;
                            i++; // Skip next char
                        }
                        // Check for string start
                        else if (c == '"')
                        {
                            FlushToken(currentToken, tokens, tokenStartLine);
                            state = State.InString;
                        }
                        // Check for identifier characters
                        else if (IsIdentifierChar(c))
                        {
                            if (currentToken.Length == 0)
                            {
                                tokenStartLine = line;
                            }
                            currentToken.Append(c);
                        }
                        // Colon is part of identifier (SHIP:VELOCITY)
                        else if (c == ':' && currentToken.Length > 0)
                        {
                            currentToken.Append(c);
                        }
                        // End of token
                        else
                        {
                            FlushToken(currentToken, tokens, tokenStartLine);
                        }
                        break;

                    case State.InLineComment:
                        if (c == '\n')
                        {
                            state = State.Normal;
                        }
                        break;

                    case State.InBlockComment:
                        if (c == '*' && next == '/')
                        {
                            state = State.Normal;
                            i++; // Skip next char
                        }
                        break;

                    case State.InString:
                        if (c == '\\' && next != '\0')
                        {
                            i++; // Skip escaped character
                        }
                        else if (c == '"')
                        {
                            state = State.Normal;
                        }
                        break;
                }
            }

            // Flush any remaining token
            FlushToken(currentToken, tokens, tokenStartLine);

            // Process tokens to identify user-defined variables and kOS identifiers
            ProcessTokens(tokens, result);

            return result;
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static void FlushToken(StringBuilder token, List<Token> tokens, int line)
        {
            if (token.Length > 0)
            {
                string text = token.ToString();
                // Remove trailing colons
                text = text.TrimEnd(':');
                if (text.Length > 0)
                {
                    tokens.Add(new Token { Text = text, Line = line });
                }
                token.Clear();
            }
        }

        private void ProcessTokens(List<Token> tokens, KosIdentifierSet result)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                string upper = token.Text.ToUpperInvariant();

                // Check for user-defined variable patterns
                if (IsUserDefinedPattern(tokens, i, out string varName, out int skip))
                {
                    result.Add(new ExtractedIdentifier(varName, true, token.Line));
                    i += skip; // Skip the variable name token we just processed
                    continue;
                }

                // Skip keywords
                if (Keywords.Contains(upper))
                {
                    continue;
                }

                // Skip numeric literals
                if (IsNumeric(token.Text))
                {
                    continue;
                }

                // Add as potential kOS identifier
                // Handle colon-separated identifiers (SHIP:VELOCITY:SURFACE)
                if (token.Text.Contains(":"))
                {
                    // Add the full path as an identifier
                    result.Add(new ExtractedIdentifier(token.Text, false, token.Line));

                    // Also add individual parts for structure validation
                    var parts = token.Text.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (!string.IsNullOrEmpty(part) && !IsNumeric(part))
                        {
                            result.Add(new ExtractedIdentifier(part, false, token.Line));
                        }
                    }
                }
                else if (token.Text.Length > 0)
                {
                    result.Add(new ExtractedIdentifier(token.Text, false, token.Line));
                }
            }
        }

        /// <summary>
        /// Check if the current position represents a user-defined variable pattern.
        /// </summary>
        private bool IsUserDefinedPattern(List<Token> tokens, int index, out string varName, out int skip)
        {
            varName = null;
            skip = 0;

            if (index >= tokens.Count)
                return false;

            string upper = tokens[index].Text.ToUpperInvariant();

            // SET x TO value
            // SET x. (short form)
            if (upper == "SET" && index + 1 < tokens.Count)
            {
                var nextToken = tokens[index + 1].Text;
                // Check it's not a colon-path (SET already exists, SHIP:VELOCITY)
                if (!nextToken.Contains(":"))
                {
                    varName = nextToken;
                    skip = 1;
                    return true;
                }
            }

            // DECLARE x
            // DECLARE PARAMETER x
            // DECLARE LOCAL x
            // DECLARE GLOBAL x
            if (upper == "DECLARE")
            {
                int varIndex = index + 1;
                // Skip optional LOCAL/GLOBAL/PARAMETER
                if (varIndex < tokens.Count)
                {
                    string nextUpper = tokens[varIndex].Text.ToUpperInvariant();
                    if (nextUpper == "LOCAL" || nextUpper == "GLOBAL" || nextUpper == "PARAMETER")
                    {
                        varIndex++;
                    }
                }
                if (varIndex < tokens.Count && !tokens[varIndex].Text.Contains(":"))
                {
                    varName = tokens[varIndex].Text;
                    skip = varIndex - index;
                    return true;
                }
            }

            // LOCAL x IS value
            // LOCAL x TO value
            if (upper == "LOCAL" && index + 1 < tokens.Count)
            {
                var nextToken = tokens[index + 1].Text;
                if (!nextToken.Contains(":") && !Keywords.Contains(nextToken.ToUpperInvariant()))
                {
                    varName = nextToken;
                    skip = 1;
                    return true;
                }
            }

            // GLOBAL x IS value
            // GLOBAL x TO value
            if (upper == "GLOBAL" && index + 1 < tokens.Count)
            {
                var nextToken = tokens[index + 1].Text;
                if (!nextToken.Contains(":") && !Keywords.Contains(nextToken.ToUpperInvariant()))
                {
                    varName = nextToken;
                    skip = 1;
                    return true;
                }
            }

            // PARAMETER x
            // PARAMETER x, y, z
            if (upper == "PARAMETER" && index + 1 < tokens.Count)
            {
                var nextToken = tokens[index + 1].Text;
                if (!nextToken.Contains(":"))
                {
                    varName = nextToken;
                    skip = 1;
                    return true;
                }
            }

            // FOR x IN collection
            if (upper == "FOR" && index + 1 < tokens.Count)
            {
                var nextToken = tokens[index + 1].Text;
                if (!nextToken.Contains(":"))
                {
                    varName = nextToken;
                    skip = 1;
                    return true;
                }
            }

            // FUNCTION name
            if (upper == "FUNCTION" && index + 1 < tokens.Count)
            {
                var nextToken = tokens[index + 1].Text;
                if (!nextToken.Contains(":"))
                {
                    varName = nextToken;
                    skip = 1;
                    return true;
                }
            }

            // LOCK x TO expression
            // But NOT: LOCK STEERING, LOCK THROTTLE, etc. (built-ins)
            if (upper == "LOCK" && index + 1 < tokens.Count)
            {
                var nextToken = tokens[index + 1].Text;
                if (!nextToken.Contains(":") && !BuiltInLockTargets.Contains(nextToken))
                {
                    varName = nextToken;
                    skip = 1;
                    return true;
                }
            }

            return false;
        }

        private static bool IsNumeric(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                if (!char.IsDigit(c) && c != '.' && c != '-' && c != 'e' && c != 'E')
                {
                    return false;
                }
            }
            return true;
        }

        private struct Token
        {
            public string Text;
            public int Line;
        }
    }
}

using System.Collections.Generic;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Types of syntax issues that can be detected in kOS scripts.
    /// </summary>
    public enum SyntaxIssueType
    {
        /// <summary>
        /// Unbalanced curly braces { }.
        /// </summary>
        UnbalancedBrace,

        /// <summary>
        /// Unbalanced parentheses ( ).
        /// </summary>
        UnbalancedParenthesis,

        /// <summary>
        /// Markdown backticks found in code (LLM artifact).
        /// </summary>
        MarkdownBacktick,

        /// <summary>
        /// Markdown bullet points found in code (LLM artifact).
        /// </summary>
        MarkdownBullet,

        /// <summary>
        /// Markdown header syntax found in code (LLM artifact).
        /// </summary>
        MarkdownHeader,

        /// <summary>
        /// Smart/curly quotes found instead of straight quotes.
        /// </summary>
        SmartQuote,

        /// <summary>
        /// Statement appears to be missing a terminator (kOS requires period).
        /// </summary>
        MissingTerminator
    }

    /// <summary>
    /// Represents a single syntax issue found in a kOS script.
    /// </summary>
    public class SyntaxIssue
    {
        /// <summary>
        /// The type of syntax issue detected.
        /// </summary>
        public SyntaxIssueType Type { get; }

        /// <summary>
        /// Line number where the issue was found (1-based).
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Human-readable description of the issue.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Create a new syntax issue.
        /// </summary>
        public SyntaxIssue(SyntaxIssueType type, int line, string message)
        {
            Type = type;
            Line = line;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            return $"Line {Line}: {Message}";
        }
    }

    /// <summary>
    /// Result of syntax checking a kOS script.
    /// </summary>
    public class KosSyntaxResult
    {
        private readonly List<SyntaxIssue> _issues;

        /// <summary>
        /// List of syntax issues found.
        /// </summary>
        public IReadOnlyList<SyntaxIssue> Issues => _issues;

        /// <summary>
        /// Whether any syntax issues were found.
        /// </summary>
        public bool HasIssues => _issues.Count > 0;

        /// <summary>
        /// Create a new syntax result.
        /// </summary>
        public KosSyntaxResult()
        {
            _issues = new List<SyntaxIssue>();
        }

        /// <summary>
        /// Add an issue to the result.
        /// </summary>
        internal void AddIssue(SyntaxIssue issue)
        {
            if (issue != null)
            {
                _issues.Add(issue);
            }
        }

        /// <summary>
        /// Add an issue to the result.
        /// </summary>
        internal void AddIssue(SyntaxIssueType type, int line, string message)
        {
            _issues.Add(new SyntaxIssue(type, line, message));
        }

        /// <summary>
        /// Create an empty result (no issues).
        /// </summary>
        public static KosSyntaxResult Empty()
        {
            return new KosSyntaxResult();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using KSPCapcom.Validation;

namespace KSPCapcom.Parsing
{
    /// <summary>
    /// Represents parsed content from an assistant message, containing interleaved
    /// prose and code block segments.
    /// </summary>
    public class ParsedMessageContent
    {
        private readonly List<MessageSegment> _segments;

        /// <summary>
        /// Ordered segments of prose and code blocks from the message.
        /// </summary>
        public IReadOnlyList<MessageSegment> Segments => _segments;

        /// <summary>
        /// Whether the message contains any code blocks.
        /// </summary>
        public bool HasCodeBlocks { get; }

        /// <summary>
        /// Number of code blocks in the message.
        /// </summary>
        public int CodeBlockCount { get; }

        internal ParsedMessageContent(List<MessageSegment> segments, int codeBlockCount)
        {
            _segments = segments ?? new List<MessageSegment>();
            CodeBlockCount = codeBlockCount;
            HasCodeBlocks = codeBlockCount > 0;
        }

        /// <summary>
        /// Create an empty parsed content (no segments).
        /// </summary>
        public static ParsedMessageContent Empty() => new ParsedMessageContent(new List<MessageSegment>(), 0);
    }

    /// <summary>
    /// Base class for a segment of parsed message content.
    /// </summary>
    public abstract class MessageSegment
    {
        /// <summary>
        /// The text content of this segment.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Start index in the original message.
        /// </summary>
        public int StartIndex { get; }

        /// <summary>
        /// End index (exclusive) in the original message.
        /// </summary>
        public int EndIndex { get; }

        protected MessageSegment(string content, int startIndex, int endIndex)
        {
            Content = content ?? string.Empty;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }
    }

    /// <summary>
    /// A segment of prose (non-code) text.
    /// </summary>
    public class ProseSegment : MessageSegment
    {
        public ProseSegment(string content, int startIndex, int endIndex)
            : base(content, startIndex, endIndex)
        {
        }
    }

    /// <summary>
    /// A segment containing a fenced code block.
    /// </summary>
    public class CodeBlockSegment : MessageSegment
    {
        /// <summary>
        /// The language tag from the fence (e.g., "kos", "kerboscript"), or null if untagged.
        /// </summary>
        public string Language { get; }

        /// <summary>
        /// The code content without fences.
        /// </summary>
        public string RawCode { get; }

        /// <summary>
        /// Whether this code block is likely kOS code based on heuristics.
        /// True if Language is "kos"/"kerboscript" or heuristic detection passes.
        /// </summary>
        public bool IsKosLikely { get; }

        /// <summary>
        /// Syntax validation result (set after validation).
        /// </summary>
        public KosSyntaxResult SyntaxResult { get; set; }

        public CodeBlockSegment(
            string content,
            int startIndex,
            int endIndex,
            string language,
            string rawCode,
            bool isKosLikely)
            : base(content, startIndex, endIndex)
        {
            Language = language;
            RawCode = rawCode ?? string.Empty;
            IsKosLikely = isKosLikely;
        }
    }

    /// <summary>
    /// Parser for extracting code blocks from markdown-formatted messages.
    /// Splits a message into prose and code block segments while preserving order.
    /// </summary>
    public class CodeBlockParser
    {
        /// <summary>
        /// Pattern to match fenced code blocks with optional language tag.
        /// Groups: lang (optional), code (content between fences).
        /// </summary>
        private static readonly Regex FencedBlockPattern = new Regex(
            @"```(?<lang>kos|kerboscript)?[ \t]*\r?\n(?<code>[\s\S]*?)```",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// kOS keywords used for heuristic detection of unlabeled code blocks.
        /// A block is considered kOS if it contains 2+ of these keywords.
        /// </summary>
        private static readonly string[] KosKeywords = new string[]
        {
            "SET", "LOCK", "UNLOCK", "UNTIL", "WAIT", "PRINT", "STAGE",
            "SHIP:", "THROTTLE", "STEERING", "ALTITUDE", "APOAPSIS", "PERIAPSIS",
            "WHEN", "THEN", "ON", "CLEARSCREEN", "LOG", "COPY", "RENAME", "DELETE",
            "DECLARE", "PARAMETER", "FUNCTION", "RETURN", "LOCAL", "GLOBAL",
            "IF", "ELSE", "FOR", "FROM", "IN", "PRESERVE", "BREAK",
            "HEADING", "PROGRADE", "RETROGRADE", "NORMAL", "ANTINORMAL",
            "RADIALOUT", "RADIALIN", "TARGET", "MANEUVER", "NODE"
        };

        /// <summary>
        /// Minimum number of kOS keywords required for heuristic detection.
        /// </summary>
        private const int MinKeywordsForHeuristic = 2;

        /// <summary>
        /// Parse a message into prose and code block segments.
        /// </summary>
        /// <param name="text">The message text to parse.</param>
        /// <returns>Parsed content with segments in order.</returns>
        public ParsedMessageContent Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return ParsedMessageContent.Empty();
            }

            var segments = new List<MessageSegment>();
            int codeBlockCount = 0;
            int lastEnd = 0;

            try
            {
                var matches = FencedBlockPattern.Matches(text);

                foreach (Match match in matches)
                {
                    // Add prose segment before this code block (if any)
                    if (match.Index > lastEnd)
                    {
                        var proseContent = text.Substring(lastEnd, match.Index - lastEnd);
                        // Only add non-whitespace prose segments
                        if (!string.IsNullOrWhiteSpace(proseContent))
                        {
                            segments.Add(new ProseSegment(proseContent, lastEnd, match.Index));
                        }
                    }

                    // Extract code block details
                    var langGroup = match.Groups["lang"];
                    var codeGroup = match.Groups["code"];

                    string language = langGroup.Success ? langGroup.Value.ToLowerInvariant() : null;
                    string rawCode = codeGroup.Success ? codeGroup.Value : string.Empty;

                    // Trim trailing whitespace from code but preserve leading indentation
                    rawCode = rawCode.TrimEnd();

                    // Determine if this is likely kOS code
                    bool isKosLikely = IsExplicitKosLanguage(language) || IsLikelyKosCode(rawCode);

                    segments.Add(new CodeBlockSegment(
                        match.Value,
                        match.Index,
                        match.Index + match.Length,
                        language,
                        rawCode,
                        isKosLikely
                    ));

                    codeBlockCount++;
                    lastEnd = match.Index + match.Length;
                }

                // Add trailing prose (if any)
                if (lastEnd < text.Length)
                {
                    var trailingProse = text.Substring(lastEnd);
                    if (!string.IsNullOrWhiteSpace(trailingProse))
                    {
                        segments.Add(new ProseSegment(trailingProse, lastEnd, text.Length));
                    }
                }
            }
            catch (Exception ex)
            {
                // On any parsing error, fall back to treating entire message as prose
                CapcomCore.LogWarning($"CodeBlockParser: Parse error - {ex.Message}");
                segments.Clear();
                segments.Add(new ProseSegment(text, 0, text.Length));
                codeBlockCount = 0;
            }

            return new ParsedMessageContent(segments, codeBlockCount);
        }

        /// <summary>
        /// Check if the language tag explicitly indicates kOS.
        /// </summary>
        private bool IsExplicitKosLanguage(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return false;
            }

            return language.Equals("kos", StringComparison.OrdinalIgnoreCase) ||
                   language.Equals("kerboscript", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Heuristic check: does this code contain enough kOS keywords to be considered kOS?
        /// Used for code blocks without an explicit language tag.
        /// </summary>
        /// <param name="code">The code content to analyze.</param>
        /// <returns>True if the code contains 2+ kOS keywords.</returns>
        public bool IsLikelyKosCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            // Convert to uppercase for case-insensitive matching
            string upperCode = code.ToUpperInvariant();
            int keywordCount = 0;

            foreach (var keyword in KosKeywords)
            {
                // Use word boundary check to avoid false positives
                // e.g., "SETTING" shouldn't match "SET"
                if (ContainsKeywordWithBoundary(upperCode, keyword))
                {
                    keywordCount++;
                    if (keywordCount >= MinKeywordsForHeuristic)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if the code contains a keyword with word boundaries.
        /// </summary>
        private bool ContainsKeywordWithBoundary(string upperCode, string keyword)
        {
            int index = 0;
            while ((index = upperCode.IndexOf(keyword, index, StringComparison.Ordinal)) >= 0)
            {
                // Check left boundary
                bool leftOk = index == 0 || !char.IsLetterOrDigit(upperCode[index - 1]);

                // Check right boundary (handle keywords ending with ':')
                int endIndex = index + keyword.Length;
                bool rightOk;
                if (keyword.EndsWith(":"))
                {
                    // For keywords like "SHIP:", we already include the colon
                    rightOk = true;
                }
                else
                {
                    rightOk = endIndex >= upperCode.Length ||
                              !char.IsLetterOrDigit(upperCode[endIndex]) ||
                              upperCode[endIndex] == ':'; // Allow "SHIP:" style
                }

                if (leftOk && rightOk)
                {
                    return true;
                }

                index++;
            }

            return false;
        }
    }
}

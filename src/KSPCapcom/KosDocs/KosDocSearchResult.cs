using System.Collections.Generic;
using System.Text;

namespace KSPCapcom.KosDocs
{
    /// <summary>
    /// Result DTO for the search_kos_docs function-calling tool.
    /// Contains the search results or error information.
    /// </summary>
    public class KosDocSearchResult
    {
        /// <summary>
        /// Maximum characters for truncated descriptions.
        /// </summary>
        public const int MaxDescriptionLength = 200;

        /// <summary>
        /// Maximum characters for truncated snippets.
        /// </summary>
        public const int MaxSnippetLength = 300;

        /// <summary>
        /// Whether the search completed successfully.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Error message if Success is false.
        /// </summary>
        public string Error { get; private set; }

        /// <summary>
        /// Search results (empty list if no matches, null if error).
        /// </summary>
        public IReadOnlyList<KosDocResultEntry> Entries { get; private set; }

        /// <summary>
        /// Original DocEntry objects used to create this result.
        /// Used by validation to track retrieved documentation.
        /// </summary>
        public IReadOnlyList<DocEntry> SourceEntries { get; private set; }

        private KosDocSearchResult() { }

        /// <summary>
        /// Create a successful result with entries.
        /// </summary>
        public static KosDocSearchResult Ok(IReadOnlyList<KosDocResultEntry> entries)
        {
            return new KosDocSearchResult
            {
                Success = true,
                Error = null,
                Entries = entries ?? new List<KosDocResultEntry>()
            };
        }

        /// <summary>
        /// Create an error result.
        /// </summary>
        public static KosDocSearchResult Fail(string error)
        {
            return new KosDocSearchResult
            {
                Success = false,
                Error = error ?? "Unknown error",
                Entries = null
            };
        }

        /// <summary>
        /// Create a successful result from DocEntry objects.
        /// </summary>
        public static KosDocSearchResult FromDocEntries(IReadOnlyList<DocEntry> entries)
        {
            var resultEntries = new List<KosDocResultEntry>();
            var sourceList = entries != null ? new List<DocEntry>(entries) : new List<DocEntry>();

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    resultEntries.Add(KosDocResultEntry.FromDocEntry(entry));
                }
            }

            var result = Ok(resultEntries);
            result.SourceEntries = sourceList;
            return result;
        }

        /// <summary>
        /// Serialize this result to JSON.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"success\":{(Success ? "true" : "false")}");

            if (!Success)
            {
                sb.Append($",\"error\":\"{JsonEscape(Error)}\"");
            }
            else
            {
                sb.Append(",\"entries\":[");
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(Entries[i].ToJson());
                }
                sb.Append("]");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// A single entry in the search results.
    /// Contains a subset of DocEntry fields suitable for LLM function calling.
    /// </summary>
    public class KosDocResultEntry
    {
        /// <summary>
        /// Unique identifier (e.g., "VESSEL:ALTITUDE").
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Entry kind (e.g., "suffix", "function", "structure").
        /// </summary>
        public string Kind { get; private set; }

        /// <summary>
        /// Truncated description (max 200 characters).
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Truncated code snippet (max 300 characters).
        /// </summary>
        public string Snippet { get; private set; }

        /// <summary>
        /// URL to official documentation.
        /// </summary>
        public string SourceRef { get; private set; }

        private KosDocResultEntry() { }

        /// <summary>
        /// Create an entry from a DocEntry.
        /// </summary>
        public static KosDocResultEntry FromDocEntry(DocEntry entry)
        {
            if (entry == null) return null;

            return new KosDocResultEntry
            {
                Id = entry.Id,
                Kind = FormatKind(entry.Type),
                Description = Truncate(entry.Description, KosDocSearchResult.MaxDescriptionLength),
                Snippet = Truncate(entry.Snippet, KosDocSearchResult.MaxSnippetLength),
                SourceRef = entry.SourceRef
            };
        }

        /// <summary>
        /// Serialize this entry to JSON.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"id\":\"{JsonEscape(Id)}\"");
            sb.Append($",\"kind\":\"{JsonEscape(Kind)}\"");

            if (!string.IsNullOrEmpty(Description))
            {
                sb.Append($",\"description\":\"{JsonEscape(Description)}\"");
            }

            if (!string.IsNullOrEmpty(Snippet))
            {
                sb.Append($",\"snippet\":\"{JsonEscape(Snippet)}\"");
            }

            if (!string.IsNullOrEmpty(SourceRef))
            {
                sb.Append($",\"sourceRef\":\"{JsonEscape(SourceRef)}\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string FormatKind(DocEntryType type)
        {
            switch (type)
            {
                case DocEntryType.Structure: return "structure";
                case DocEntryType.Suffix: return "suffix";
                case DocEntryType.Function: return "function";
                case DocEntryType.Keyword: return "keyword";
                case DocEntryType.Constant: return "constant";
                case DocEntryType.Command: return "command";
                default: return "unknown";
            }
        }

        /// <summary>
        /// Truncate a string to a maximum length, adding ellipsis if truncated.
        /// </summary>
        internal static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length <= maxLength) return value;
            return value.Substring(0, maxLength - 3) + "...";
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}

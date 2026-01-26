using System.Collections.Generic;

namespace KSPCapcom.KosDocs
{
    /// <summary>
    /// Entry type for kOS documentation.
    /// </summary>
    public enum DocEntryType
    {
        Structure,
        Suffix,
        Function,
        Keyword,
        Constant,
        Command
    }

    /// <summary>
    /// Access mode for suffixes and methods.
    /// </summary>
    public enum DocAccessMode
    {
        None,
        Get,
        Set,
        GetSet,
        Method
    }

    /// <summary>
    /// A single entry in the kOS documentation index.
    /// Represents structures, suffixes, functions, keywords, constants, or commands.
    /// </summary>
    public class DocEntry
    {
        /// <summary>
        /// Unique identifier (e.g., "VESSEL:ALTITUDE", "LOCK", "FUNCTION:ABS").
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name (e.g., "ALTITUDE").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Entry type: structure, suffix, function, keyword, constant, command.
        /// </summary>
        public DocEntryType Type { get; set; }

        /// <summary>
        /// Parent structure for suffixes (e.g., "VESSEL"). Null for top-level items.
        /// </summary>
        public string ParentStructure { get; set; }

        /// <summary>
        /// Return type (e.g., "Scalar", "Vector", "List"). Null if not applicable.
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// Access mode: get, set, get/set, method. None if not applicable.
        /// </summary>
        public DocAccessMode Access { get; set; }

        /// <summary>
        /// Method signature (e.g., "CREW()", "ABS(value)"). Null if not a method.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// Human-readable explanation.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Example kOS code snippet.
        /// </summary>
        public string Snippet { get; set; }

        /// <summary>
        /// URL to official documentation.
        /// </summary>
        public string SourceRef { get; set; }

        /// <summary>
        /// Category tags for retrieval (e.g., "vessel", "orbit", "math").
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Alternative names (e.g., "SHIP" for "VESSEL").
        /// </summary>
        public List<string> Aliases { get; set; } = new List<string>();

        /// <summary>
        /// Cross-reference IDs to related entries.
        /// </summary>
        public List<string> Related { get; set; } = new List<string>();

        /// <summary>
        /// Whether this entry is deprecated.
        /// </summary>
        public bool Deprecated { get; set; }

        /// <summary>
        /// Migration guidance for deprecated entries.
        /// </summary>
        public string DeprecationNote { get; set; }

        /// <summary>
        /// Category grouping (e.g., "Vessel Properties", "Orbital Mechanics").
        /// Used for organizing retrieval results.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Usage frequency hint: "common", "moderate", or "rare".
        /// Helps prioritize retrieval results.
        /// </summary>
        public string UsageFrequency { get; set; }

        /// <summary>
        /// Create an empty DocEntry.
        /// </summary>
        public DocEntry()
        {
        }

        /// <summary>
        /// Format this entry for inclusion in an LLM prompt.
        /// Produces a concise summary suitable for RAG context.
        /// </summary>
        public string ToPromptFormat()
        {
            var parts = new List<string>();

            // Header with type and name
            string typeLabel = Type.ToString().ToUpperInvariant();
            if (!string.IsNullOrEmpty(ParentStructure))
            {
                parts.Add($"[{typeLabel}] {ParentStructure}:{Name}");
            }
            else
            {
                parts.Add($"[{typeLabel}] {Name}");
            }

            // Signature if available
            if (!string.IsNullOrEmpty(Signature))
            {
                parts.Add($"Signature: {Signature}");
            }

            // Return type and access
            var accessInfo = new List<string>();
            if (!string.IsNullOrEmpty(ReturnType))
            {
                accessInfo.Add($"Returns: {ReturnType}");
            }
            if (Access != DocAccessMode.None)
            {
                accessInfo.Add($"Access: {FormatAccess(Access)}");
            }
            if (accessInfo.Count > 0)
            {
                parts.Add(string.Join(", ", accessInfo));
            }

            // Description
            if (!string.IsNullOrEmpty(Description))
            {
                parts.Add(Description);
            }

            // Snippet
            if (!string.IsNullOrEmpty(Snippet))
            {
                parts.Add($"Example:\n{Snippet}");
            }

            // Deprecation warning
            if (Deprecated)
            {
                string warning = "⚠️ DEPRECATED";
                if (!string.IsNullOrEmpty(DeprecationNote))
                {
                    warning += $": {DeprecationNote}";
                }
                parts.Add(warning);
            }

            return string.Join("\n", parts);
        }

        private static string FormatAccess(DocAccessMode access)
        {
            switch (access)
            {
                case DocAccessMode.Get: return "get";
                case DocAccessMode.Set: return "set";
                case DocAccessMode.GetSet: return "get/set";
                case DocAccessMode.Method: return "method";
                default: return "";
            }
        }

        public override string ToString()
        {
            return $"{Type}: {Id}";
        }
    }
}

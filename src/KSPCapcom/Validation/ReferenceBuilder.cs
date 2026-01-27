using System.Collections.Generic;
using System.Text;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Builds a "References" section for LLM responses in grounded mode.
    /// Cites kOS documentation sources that support the generated code.
    /// </summary>
    public static class ReferenceBuilder
    {
        /// <summary>
        /// Maximum length for description text in references.
        /// </summary>
        public const int MaxDescriptionLength = 60;

        /// <summary>
        /// Build a references section from validation results and doc tracker.
        /// </summary>
        /// <param name="validation">The validation result with verified identifiers.</param>
        /// <param name="docTracker">The doc tracker with retrieved entries.</param>
        /// <returns>A formatted references section, or empty string if no references needed.</returns>
        public static string Build(KosValidationResult validation, DocEntryTracker docTracker)
        {
            // No validation performed - skip references
            if (validation == null)
            {
                return string.Empty;
            }

            // No verified identifiers - check if we should show a warning
            if (validation.Verified.Count == 0)
            {
                // If there are unverified identifiers but no verified ones, warn
                if (validation.HasUnverifiedIdentifiers)
                {
                    return "## References\n\n_No documentation references available for the identifiers in this code._";
                }
                return string.Empty;
            }

            // Build reference entries, deduplicating by DocEntry ID
            var seenDocIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var references = new List<ReferenceEntry>();

            foreach (var verified in validation.Verified)
            {
                // Try to find the source doc
                DocEntry sourceDoc = verified.SourceDoc;
                if (sourceDoc == null && docTracker != null)
                {
                    sourceDoc = docTracker.FindByIdentifier(verified.Identifier);
                }

                // Create reference entry
                var entry = new ReferenceEntry
                {
                    Identifier = verified.Identifier,
                    DocEntryId = verified.DocEntryId,
                    SourceRef = verified.SourceRef
                };

                if (sourceDoc != null)
                {
                    entry.Description = TruncateDescription(sourceDoc.Description);
                    entry.SourceRef = sourceDoc.SourceRef ?? entry.SourceRef;

                    // Deduplicate by doc ID
                    if (!string.IsNullOrEmpty(sourceDoc.Id) && !seenDocIds.Add(sourceDoc.Id))
                    {
                        continue; // Already included this doc
                    }
                }
                else
                {
                    // No doc found - still include the identifier with what we have
                    if (!string.IsNullOrEmpty(verified.DocEntryId) && !seenDocIds.Add(verified.DocEntryId))
                    {
                        continue;
                    }
                }

                references.Add(entry);
            }

            // No unique references to show
            if (references.Count == 0)
            {
                return string.Empty;
            }

            // Build the references section
            var sb = new StringBuilder();
            sb.AppendLine("## References");
            sb.AppendLine();

            foreach (var entry in references)
            {
                sb.Append("- `");
                sb.Append(entry.Identifier);
                sb.Append("`");

                if (!string.IsNullOrEmpty(entry.Description))
                {
                    sb.Append(" - ");
                    sb.Append(entry.Description);
                }

                if (!string.IsNullOrEmpty(entry.SourceRef))
                {
                    sb.Append(" ([docs](");
                    sb.Append(entry.SourceRef);
                    sb.Append("))");
                }
                else
                {
                    sb.Append(" (local docs)");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Truncate a description to the maximum length.
        /// </summary>
        private static string TruncateDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return null;
            }

            // Clean up whitespace
            description = description.Replace("\r\n", " ").Replace("\n", " ").Trim();

            if (description.Length <= MaxDescriptionLength)
            {
                return description;
            }

            // Truncate and add ellipsis
            return description.Substring(0, MaxDescriptionLength - 3).TrimEnd() + "...";
        }

        /// <summary>
        /// Internal class to hold reference entry data.
        /// </summary>
        private class ReferenceEntry
        {
            public string Identifier { get; set; }
            public string DocEntryId { get; set; }
            public string Description { get; set; }
            public string SourceRef { get; set; }
        }
    }
}

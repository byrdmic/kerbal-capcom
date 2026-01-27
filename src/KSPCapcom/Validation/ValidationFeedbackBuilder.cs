using System;
using System.Text;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Builds validation feedback text for LLM responses in grounded mode.
    /// Produces either a warning block for unverified identifiers or a success footer.
    /// </summary>
    public static class ValidationFeedbackBuilder
    {
        /// <summary>
        /// Maximum number of unverified identifiers to show before truncating.
        /// </summary>
        public const int MaxUnverifiedToShow = 10;

        /// <summary>
        /// Maximum number of suggestions to show per unverified identifier.
        /// </summary>
        public const int MaxSuggestionsPerIdentifier = 3;

        /// <summary>
        /// Build validation feedback from a validation result.
        /// </summary>
        /// <param name="validation">The validation result to build feedback from.</param>
        /// <returns>A formatted feedback block, or empty string if no feedback needed.</returns>
        public static string Build(KosValidationResult validation)
        {
            if (validation == null)
            {
                return string.Empty;
            }

            // Check for warning first - show it even if there's a validation warning message
            if (!string.IsNullOrEmpty(validation.Warning))
            {
                // Validation was skipped with a warning - no feedback to show
                return string.Empty;
            }

            if (validation.HasUnverifiedIdentifiers)
            {
                return BuildWarning(validation);
            }

            if (validation.Verified.Count > 0)
            {
                return BuildSuccess(validation);
            }

            return string.Empty;
        }

        /// <summary>
        /// Build a warning block for unverified identifiers.
        /// </summary>
        private static string BuildWarning(KosValidationResult validation)
        {
            var sb = new StringBuilder();

            sb.AppendLine("---");
            sb.AppendLine("**Grounded Check Failed**");
            sb.AppendLine();
            sb.AppendLine("The following kOS identifiers could not be verified against documentation:");

            int shown = 0;
            foreach (var unverified in validation.Unverified)
            {
                if (shown >= MaxUnverifiedToShow)
                {
                    int remaining = validation.Unverified.Count - MaxUnverifiedToShow;
                    sb.AppendLine($"- ...and {remaining} more");
                    break;
                }

                sb.Append($"- `{unverified.Identifier}` (line {unverified.Line})");

                // Add suggestions if available
                if (unverified.SuggestedMatches.Count > 0)
                {
                    int suggestionCount = Math.Min(unverified.SuggestedMatches.Count, MaxSuggestionsPerIdentifier);
                    var suggestions = new string[suggestionCount];
                    for (int i = 0; i < suggestionCount; i++)
                    {
                        suggestions[i] = unverified.SuggestedMatches[i];
                    }
                    sb.Append($" - did you mean: {string.Join(", ", suggestions)}?");
                }

                sb.AppendLine();
                shown++;
            }

            sb.AppendLine();
            sb.Append("Please verify these identifiers manually before use, or ask me to search the docs for the correct syntax.");

            return sb.ToString();
        }

        /// <summary>
        /// Build a success footer when all identifiers are verified.
        /// </summary>
        private static string BuildSuccess(KosValidationResult validation)
        {
            return "---\n**Grounded** - All kOS identifiers verified against documentation.";
        }
    }
}

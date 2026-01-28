using System;
using System.Text;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Builds validation feedback text for LLM responses in grounded mode.
    /// Produces either a warning block for validation issues or a success footer.
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
        /// Maximum number of syntax issues to show before truncating.
        /// </summary>
        public const int MaxSyntaxIssuesToShow = 5;

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

            // Check if we need to show any issues
            bool hasSyntaxIssues = validation.HasSyntaxIssues;
            bool hasUnverifiedIdentifiers = validation.HasUnverifiedIdentifiers;

            if (hasSyntaxIssues || hasUnverifiedIdentifiers)
            {
                return BuildCombinedWarning(validation);
            }

            if (validation.Verified.Count > 0)
            {
                return BuildSuccess(validation);
            }

            return string.Empty;
        }

        /// <summary>
        /// Build a combined warning block for syntax issues and/or unverified identifiers.
        /// </summary>
        private static string BuildCombinedWarning(KosValidationResult validation)
        {
            var sb = new StringBuilder();

            sb.AppendLine("---");
            sb.AppendLine("**Script Validation Failed**");
            sb.AppendLine();
            sb.AppendLine("Issues detected:");

            // Add syntax issues first
            if (validation.HasSyntaxIssues)
            {
                int shown = 0;
                foreach (var issue in validation.SyntaxResult.Issues)
                {
                    if (shown >= MaxSyntaxIssuesToShow)
                    {
                        int remaining = validation.SyntaxResult.Issues.Count - MaxSyntaxIssuesToShow;
                        sb.AppendLine($"- ...and {remaining} more syntax issue(s)");
                        break;
                    }

                    sb.AppendLine($"- Line {issue.Line}: {issue.Message}");
                    shown++;
                }
            }

            // Add unverified identifiers
            if (validation.HasUnverifiedIdentifiers)
            {
                int shown = 0;
                foreach (var unverified in validation.Unverified)
                {
                    if (shown >= MaxUnverifiedToShow)
                    {
                        int remaining = validation.Unverified.Count - MaxUnverifiedToShow;
                        sb.AppendLine($"- ...and {remaining} more unverified identifier(s)");
                        break;
                    }

                    sb.Append($"- `{unverified.Identifier}` (line {unverified.Line}) - unverified identifier");

                    // Add suggestions if available
                    if (unverified.SuggestedMatches.Count > 0)
                    {
                        int suggestionCount = Math.Min(unverified.SuggestedMatches.Count, MaxSuggestionsPerIdentifier);
                        var suggestions = new string[suggestionCount];
                        for (int i = 0; i < suggestionCount; i++)
                        {
                            suggestions[i] = unverified.SuggestedMatches[i];
                        }
                        sb.Append($" (did you mean: {string.Join(", ", suggestions)}?)");
                    }

                    sb.AppendLine();
                    shown++;
                }
            }

            sb.AppendLine();
            sb.Append("**To fix:** Ask me to \"fix the script issues\" or \"regenerate the script\"");

            return sb.ToString();
        }

        /// <summary>
        /// Build a success footer when all identifiers are verified and no syntax issues.
        /// </summary>
        private static string BuildSuccess(KosValidationResult validation)
        {
            return "---\n**Grounded** - All kOS identifiers verified against documentation.";
        }
    }
}

using System;
using System.Text;
using KSPCapcom.Editor;

namespace KSPCapcom.Critique
{
    /// <summary>
    /// Builds critique-specific system prompts for design feedback requests.
    /// Generates structured output format instructions for the LLM.
    /// </summary>
    public class CritiquePromptBuilder
    {
        /// <summary>
        /// Version identifier for the critique prompt format.
        /// Increment when making significant changes to prompt structure.
        /// </summary>
        public const string Version = "1.0.0";

        /// <summary>
        /// Core identity for the critique mode.
        /// </summary>
        private const string CoreIdentity =
            "You are CAPCOM's spacecraft design reviewer for Kerbal Space Program. " +
            "Analyze the provided craft data and give focused, actionable feedback.";

        /// <summary>
        /// Output format instructions for consistent structured responses.
        /// </summary>
        private const string OutputFormatInstructions =
            "RESPONSE FORMAT:\n" +
            "Provide exactly 3 critical issues and exactly 3 improvement suggestions.\n\n" +
            "CRITICAL ISSUES (top 3 problems to fix):\n" +
            "1. [Issue description referencing specific data, e.g., \"TWR of 0.8 is too low for Kerbin launch\"]\n" +
            "2. [Issue]\n" +
            "3. [Issue]\n\n" +
            "IMPROVEMENTS (top 3 suggestions):\n" +
            "1. [Improvement with specific guidance]\n" +
            "2. [Improvement]\n" +
            "3. [Improvement]";

        /// <summary>
        /// Guidelines for referencing snapshot data.
        /// </summary>
        private const string DataReferenceGuidelines =
            "IMPORTANT: Reference specific values from the craft data. " +
            "Say \"TWR of 0.8\" not \"low TWR\". Say \"45 parts\" not \"many parts\". " +
            "Use KSP terminology (periapsis, delta-v, TWR, staging). " +
            "Keep each point to 1-2 sentences. Be direct and specific.";

        /// <summary>
        /// Build a critique system prompt that includes the craft snapshot.
        /// </summary>
        /// <param name="snapshot">The craft snapshot to analyze.</param>
        /// <returns>The complete system prompt for critique mode.</returns>
        /// <exception cref="ArgumentNullException">Thrown if snapshot is null.</exception>
        public string BuildCritiquePrompt(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();

            // 1. Core identity
            builder.AppendLine(CoreIdentity);
            builder.AppendLine();

            // 2. Craft data section
            builder.AppendLine("CRAFT DATA:");
            builder.AppendLine(snapshot.ToPromptSummary());
            builder.AppendLine();

            // 3. Data reference guidelines
            builder.AppendLine(DataReferenceGuidelines);
            builder.AppendLine();

            // 4. Output format instructions
            builder.Append(OutputFormatInstructions);

            return builder.ToString();
        }

        /// <summary>
        /// Build a simple user message for the critique request.
        /// The actual analysis context is in the system prompt.
        /// </summary>
        public string BuildCritiqueUserMessage()
        {
            return "Analyze this spacecraft design and provide your critique.";
        }
    }
}

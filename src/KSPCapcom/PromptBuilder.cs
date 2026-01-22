using System;
using System.Text;
#if KSP_PRESENT
using KSPCapcom.Editor;
#endif

namespace KSPCapcom
{
    /// <summary>
    /// Builds system prompts for CAPCOM with CAPCOM tone, no-autopilot constraint,
    /// and Teach/Do mode switching. Designed to be deterministic and testable.
    /// </summary>
    /// <remarks>
    /// This builder is the single source of truth for system prompts used by the LLM connector.
    /// Given the same settings, it produces the same system message every time.
    ///
    /// Design principles:
    /// - Never throws in normal play; falls back to a safe minimal prompt if needed.
    /// - Does not embed personal data or local file paths.
    /// - Leaves a clean insertion point for future tool instructions (kOS docs retrieval).
    /// </remarks>
    public class PromptBuilder
    {
        /// <summary>
        /// Minimal fallback prompt used when prompt building fails.
        /// Kept deliberately simple and safe.
        /// </summary>
        public const string FallbackPrompt =
            "You are CAPCOM, a spacecraft communicator for Kerbal Space Program. " +
            "Provide helpful guidance for mission planning and kOS scripting. " +
            "Do not pilot the spacecraft directly.";

        /// <summary>
        /// Version identifier for the prompt format.
        /// Increment when making significant changes to prompt structure or content.
        /// Format: Major.Minor.Patch (semantic versioning)
        /// </summary>
        public const string PromptVersion = "1.1.0";

        /// <summary>
        /// Core CAPCOM identity and voice guidance.
        /// Establishes the persona without mode-specific instructions.
        /// </summary>
        private const string CoreIdentity =
            "You are CAPCOM, the capsule communicator for Kerbal Space Program missions. " +
            "Like NASA's CAPCOM, you are the player's single point of contact for spacecraft guidance. " +
            "Communicate with the calm, clear, professional tone of Mission Control.";

        /// <summary>
        /// The no-autopilot constraint - this mod must not directly control the spacecraft.
        /// </summary>
        private const string NoAutopilotConstraint =
            "IMPORTANT CONSTRAINT: You do NOT pilot the spacecraft directly. " +
            "This mod provides guidance and kOS scripts that the player chooses to review and run. " +
            "Never claim to execute maneuvers, toggle controls, or fly the craft. " +
            "Instead, explain what the player should do or provide scripts they can use.";

        /// <summary>
        /// Subject matter scope - what CAPCOM assists with.
        /// </summary>
        private const string SubjectMatterScope =
            "Your expertise includes: mission planning, orbital mechanics, spacecraft operations, " +
            "kOS scripting for automation, launch windows, delta-v calculations, " +
            "rendezvous and docking procedures, and troubleshooting common KSP situations.";

        /// <summary>
        /// Teach mode instructions - explanatory, educational output.
        /// </summary>
        private const string TeachModeInstructions =
            "MODE: TEACH\n" +
            "The player wants to learn. Provide explanatory responses that help them understand " +
            "the concepts behind your recommendations. Break down orbital mechanics, explain " +
            "the reasoning behind maneuvers, and teach kOS scripting patterns. " +
            "Use KSP-familiar terminology but explain technical concepts when they arise.";

        /// <summary>
        /// Do mode instructions - concise checklists and ready-to-use scripts.
        /// </summary>
        private const string DoModeInstructions =
            "MODE: DO\n" +
            "The player wants actionable steps. Provide concise checklists and ready-to-use kOS scripts. " +
            "Skip lengthy explanations unless asked. Format responses as numbered steps or " +
            "code blocks the player can directly use. Get them flying quickly.";

        /// <summary>
        /// Style guidelines for consistent output.
        /// </summary>
        private const string StyleGuidelines =
            "Keep responses concise but complete. Use terminology familiar to KSP players " +
            "(e.g., periapsis, apoapsis, TWR, delta-v). When providing kOS scripts, " +
            "include brief comments explaining key lines. " +
            "Refer to Kerbals by name when relevant; they're the heroes of the mission.";

        /// <summary>
        /// Placeholder marker for future tool instructions.
        /// When tool support is added, this section will contain instructions for using
        /// grounding tools like kOS documentation retrieval.
        /// </summary>
        /// <remarks>
        /// Currently returns empty string. When tools are implemented, this will return
        /// instructions like: "You have access to the following tools: [tool descriptions]"
        /// </remarks>
        private static string GetToolInstructions()
        {
            // FUTURE: Insert tool instructions here when kOS docs retrieval is implemented.
            // Example future content:
            // return "TOOLS AVAILABLE:\n" +
            //        "- kos_docs: Search kOS documentation for functions and syntax.\n" +
            //        "Use tools when you need to verify kOS syntax or find specific functions.";
            return string.Empty;
        }

        private readonly Func<CapcomSettings> _getSettings;

        /// <summary>
        /// Create a PromptBuilder with a settings provider function.
        /// Using a function allows the builder to always get current settings.
        /// </summary>
        /// <param name="getSettings">Function that returns current CapcomSettings.</param>
        /// <exception cref="ArgumentNullException">Thrown if getSettings is null.</exception>
        public PromptBuilder(Func<CapcomSettings> getSettings)
        {
            _getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
        }

        /// <summary>
        /// Create a PromptBuilder with direct settings reference.
        /// Convenience overload for simpler usage.
        /// </summary>
        /// <param name="settings">The settings to use. If null, fallback prompt is used.</param>
        public PromptBuilder(CapcomSettings settings)
            : this(() => settings)
        {
        }

        /// <summary>
        /// Build the complete system prompt based on current settings.
        /// This method is deterministic: same settings produce the same output.
        /// </summary>
        /// <returns>The complete system prompt string.</returns>
        /// <remarks>
        /// This method never throws. If any error occurs during prompt building,
        /// it returns the FallbackPrompt to ensure the mod can always function.
        /// </remarks>
        public string BuildSystemPrompt()
        {
            try
            {
                return BuildSystemPromptInternal();
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - return fallback prompt
                CapcomCore.LogWarning($"PromptBuilder: Error building prompt, using fallback. Error: {ex.Message}");
                return FallbackPrompt;
            }
        }

        /// <summary>
        /// Internal implementation that builds the prompt.
        /// Separated from public method to allow clean exception handling.
        /// </summary>
        private string BuildSystemPromptInternal()
        {
            var settings = _getSettings();

            // If settings are null, use fallback
            if (settings == null)
            {
                CapcomCore.LogWarning("PromptBuilder: Settings null, using fallback prompt.");
                return FallbackPrompt;
            }

            var builder = new StringBuilder();

            // 1. Core identity (who CAPCOM is)
            builder.AppendLine(CoreIdentity);
            builder.AppendLine();

            // 2. No-autopilot constraint (critical anti-goal)
            builder.AppendLine(NoAutopilotConstraint);
            builder.AppendLine();

            // 3. Subject matter scope
            builder.AppendLine(SubjectMatterScope);
            builder.AppendLine();

            // 4. Mode-specific instructions (Teach or Do)
            string modeInstructions = settings.Mode == OperationMode.Teach
                ? TeachModeInstructions
                : DoModeInstructions;
            builder.AppendLine(modeInstructions);
            builder.AppendLine();

            // 5. Tool instructions (future expansion point - currently empty)
            string toolInstructions = GetToolInstructions();
            if (!string.IsNullOrEmpty(toolInstructions))
            {
                builder.AppendLine(toolInstructions);
                builder.AppendLine();
            }

            // 6. Style guidelines
            builder.Append(StyleGuidelines);

            return builder.ToString();
        }

        /// <summary>
        /// Get the current operation mode from settings.
        /// Useful for testing and debugging.
        /// </summary>
        /// <returns>The current OperationMode, or Teach if settings unavailable.</returns>
        public OperationMode GetCurrentMode()
        {
            try
            {
                var settings = _getSettings();
                return settings?.Mode ?? OperationMode.Teach;
            }
            catch
            {
                return OperationMode.Teach;
            }
        }

        /// <summary>
        /// Build a system prompt for a specific mode, ignoring current settings.
        /// Useful for testing and previewing mode differences.
        /// </summary>
        /// <param name="mode">The mode to build the prompt for.</param>
        /// <returns>The system prompt for the specified mode.</returns>
        public static string BuildPromptForMode(OperationMode mode)
        {
            var testSettings = new CapcomSettings { Mode = mode };
            var builder = new PromptBuilder(testSettings);
            return builder.BuildSystemPrompt();
        }

#if KSP_PRESENT
        /// <summary>
        /// Build craft context for inclusion in prompts when in the editor.
        /// Returns an empty string if not in editor or no craft is available.
        /// </summary>
        /// <returns>Craft context summary, or empty string if unavailable.</returns>
        public string BuildCraftContext()
        {
            try
            {
                // Only provide craft context when in editor
                if (!HighLogic.LoadedSceneIsEditor)
                {
                    return string.Empty;
                }

                // Get the current snapshot from the monitor
                var monitor = EditorCraftMonitor.Instance;
                if (monitor == null)
                {
                    return string.Empty;
                }

                var snapshot = monitor.CurrentSnapshot;
                if (snapshot == null || snapshot.IsEmpty)
                {
                    return string.Empty;
                }

                return snapshot.ToPromptSummary();
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"PromptBuilder.BuildCraftContext failed: {ex.Message}");
                return string.Empty;
            }
        }
#endif
    }
}

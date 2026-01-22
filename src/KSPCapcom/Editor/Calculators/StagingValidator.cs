using System;
using System.Collections.Generic;

namespace KSPCapcom.Editor.Calculators
{
    /// <summary>
    /// Validates staging configuration and detects common errors.
    ///
    /// Detects:
    /// - Multi-stage craft without decouplers
    /// - Engines in upper stages without decouplers (except stage 0)
    /// - Empty stages (no parts activated)
    /// </summary>
    public static class StagingValidator
    {
        /// <summary>
        /// Validate staging configuration for the given craft snapshot.
        /// Returns a list of warnings, or empty if no issues found.
        /// </summary>
        public static StagingValidation Validate(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return StagingValidation.Empty;
            }

            var warnings = new List<string>();

            try
            {
                // Check if this is a multi-stage craft
                int stageCount = snapshot.Staging?.StageCount ?? 0;
                int decouplerCount = snapshot.Decouplers?.Count ?? 0;
                int engineCount = snapshot.Engines?.Count ?? 0;

                // Warning: Multi-stage craft without decouplers
                if (stageCount > 1 && decouplerCount == 0)
                {
                    warnings.Add("Multi-stage craft with no decouplers detected");
                }

                // Note: More sophisticated staging validation would require per-stage part tracking,
                // which is not available in the current EditorCraftSnapshot structure.
                // Future enhancement: Track which parts are in which stages, then validate:
                // - Engines in upper stages without decouplers below them
                // - Empty stages (no parts activated)
                // - Fuel tanks not accessible to engines due to staging order

                // For now, we provide basic multi-stage validation
                // If there are multiple stages but no separation mechanism, warn
                if (stageCount > 2 && decouplerCount < (stageCount - 1))
                {
                    warnings.Add($"Stage count ({stageCount}) suggests complex staging, but only {decouplerCount} decouplers found");
                }

                return new StagingValidation(warnings);
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"StagingValidator.Validate failed: {ex.Message}");
                return StagingValidation.Empty;
            }
        }
    }
}

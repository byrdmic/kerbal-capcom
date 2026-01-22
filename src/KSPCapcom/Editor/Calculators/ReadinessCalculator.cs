using System;

namespace KSPCapcom.Editor.Calculators
{
    /// <summary>
    /// Master orchestrator for calculating all readiness metrics from an EditorCraftSnapshot.
    /// Delegates to individual calculators and assembles results.
    /// Never throws; returns NotAvailable on errors.
    /// </summary>
    public static class ReadinessCalculator
    {
        /// <summary>
        /// Calculate all readiness metrics for the given craft snapshot.
        /// </summary>
        /// <param name="snapshot">The craft snapshot to analyze.</param>
        /// <returns>ReadinessMetrics containing all derived metrics, or NotAvailable on error.</returns>
        public static ReadinessMetrics Calculate(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return ReadinessMetrics.NotAvailable;
            }

            try
            {
                var twr = TWRCalculator.Calculate(snapshot);
                var deltaV = DeltaVCalculator.Calculate(snapshot);
                var controlAuthority = ControlAuthorityCalculator.Calculate(snapshot);
                var staging = StagingValidator.Validate(snapshot);

                return new ReadinessMetrics(twr, deltaV, controlAuthority, staging);
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"ReadinessCalculator.Calculate failed: {ex.Message}");
                return ReadinessMetrics.NotAvailable;
            }
        }
    }
}

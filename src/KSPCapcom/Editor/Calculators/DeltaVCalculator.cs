using System;

namespace KSPCapcom.Editor.Calculators
{
    /// <summary>
    /// Calculates delta-V estimates using the Tsiolkovsky rocket equation.
    /// Formula: ΔV = Isp * g₀ * ln(m_wet / m_dry)
    ///
    /// MVP: Single-stage calculation using total mass.
    /// Future: Per-stage calculation with fuel flow tracking.
    /// </summary>
    public static class DeltaVCalculator
    {
        /// <summary>
        /// Standard gravity constant (m/s²).
        /// </summary>
        private const float STANDARD_GRAVITY_G0 = 9.82f;

        /// <summary>
        /// Calculate delta-V estimate for the given craft snapshot.
        /// Returns NotAvailable if no engines, no fuel, or invalid mass ratio.
        /// </summary>
        public static DeltaVEstimate Calculate(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return DeltaVEstimate.NotAvailable;
            }

            // Need engines to have delta-V
            if (snapshot.Engines.Count == 0)
            {
                return DeltaVEstimate.NotAvailable;
            }

            // Need fuel (wet mass > dry mass)
            if (snapshot.TotalMass <= snapshot.TotalDryMass || snapshot.TotalDryMass <= 0f)
            {
                return DeltaVEstimate.NotAvailable;
            }

            try
            {
                // Calculate average ISP across all engines (vacuum ISP)
                float totalIsp = 0f;
                int engineCount = 0;

                foreach (var engine in snapshot.Engines)
                {
                    if (engine.VacuumIsp > 0f)
                    {
                        totalIsp += engine.VacuumIsp;
                        engineCount++;
                    }
                }

                if (engineCount == 0)
                {
                    return DeltaVEstimate.NotAvailable;
                }

                float avgIsp = totalIsp / engineCount;

                // Tsiolkovsky equation: ΔV = Isp * g₀ * ln(m_wet / m_dry)
                float massRatio = snapshot.TotalMass / snapshot.TotalDryMass;

                // Natural logarithm
                float lnMassRatio = (float)Math.Log(massRatio);

                float deltaV = avgIsp * STANDARD_GRAVITY_G0 * lnMassRatio;

                // Sanity check: delta-V should be positive and reasonable
                if (deltaV < 0f || deltaV > 100000f)
                {
                    return DeltaVEstimate.NotAvailable;
                }

                return new DeltaVEstimate(totalDeltaV: deltaV);
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"DeltaVCalculator.Calculate failed: {ex.Message}");
                return DeltaVEstimate.NotAvailable;
            }
        }
    }
}

using System;

namespace KSPCapcom.Editor.Calculators
{
    /// <summary>
    /// Calculates Thrust-to-Weight Ratio (TWR) for stage 0 of a craft.
    /// Formula: TWR = Thrust / (Mass * g₀)
    /// </summary>
    public static class TWRCalculator
    {
        /// <summary>
        /// Kerbin standard gravity at sea level (m/s²).
        /// </summary>
        private const float KERBIN_GRAVITY = 9.82f;

        /// <summary>
        /// Calculate TWR analysis for the given craft snapshot.
        /// Returns NotAvailable if no engines or invalid mass.
        /// </summary>
        public static TWRAnalysis Calculate(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return TWRAnalysis.NotAvailable;
            }

            // Need engines and valid mass
            if (snapshot.Engines.Count == 0)
            {
                return TWRAnalysis.NotAvailable;
            }

            if (snapshot.TotalMass <= 0f)
            {
                return TWRAnalysis.NotAvailable;
            }

            try
            {
                // Sum thrust from all engines
                float totalVacuumThrust = 0f;
                float totalAtmosphericThrust = 0f;

                foreach (var engine in snapshot.Engines)
                {
                    // Vacuum thrust is the max thrust
                    totalVacuumThrust += engine.MaxThrust;

                    // Atmospheric thrust is scaled by ISP ratio
                    // AtmosphericThrust = MaxThrust * (AtmIsp / VacIsp)
                    if (engine.VacuumIsp > 0f)
                    {
                        float ispRatio = engine.AtmosphericIsp / engine.VacuumIsp;
                        totalAtmosphericThrust += engine.MaxThrust * ispRatio;
                    }
                    else
                    {
                        // Fallback: assume atmospheric thrust equals vacuum thrust
                        totalAtmosphericThrust += engine.MaxThrust;
                    }
                }

                // Calculate TWR
                // TWR = Thrust / (Mass * g₀)
                float weight = snapshot.TotalMass * KERBIN_GRAVITY;
                float vacuumTWR = totalVacuumThrust / weight;
                float atmosphericTWR = totalAtmosphericThrust / weight;

                // Check if can launch from Kerbin (ASL TWR >= 1.0)
                bool canLaunch = atmosphericTWR >= 1.0f;

                return new TWRAnalysis(
                    vacuumTWR: vacuumTWR,
                    atmosphericTWR: atmosphericTWR,
                    engineCount: snapshot.Engines.Count,
                    canLaunchFromKerbin: canLaunch
                );
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"TWRCalculator.Calculate failed: {ex.Message}");
                return TWRAnalysis.NotAvailable;
            }
        }
    }
}

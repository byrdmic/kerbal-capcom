using System;

namespace KSPCapcom.Editor.Calculators
{
    /// <summary>
    /// Analyzes control authority based on presence of command pods, reaction wheels, and RCS.
    ///
    /// Status Logic:
    /// - None: No command pod (uncontrollable)
    /// - Marginal: Command pod only, no reaction wheels or RCS (limited maneuverability)
    /// - Good: Command pod with reaction wheels or RCS (full control)
    /// </summary>
    public static class ControlAuthorityCalculator
    {
        /// <summary>
        /// Calculate control authority check for the given craft snapshot.
        /// </summary>
        public static ControlAuthorityCheck Calculate(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return ControlAuthorityCheck.NotAvailable;
            }

            try
            {
                int commandPods = 0;
                int reactionWheels = 0;
                int rcsCount = 0;

                foreach (var control in snapshot.ControlParts)
                {
                    switch (control.Type)
                    {
                        case ControlType.CommandPod:
                            commandPods++;
                            break;
                        case ControlType.ReactionWheel:
                            reactionWheels++;
                            break;
                        case ControlType.RCS:
                            rcsCount++;
                            break;
                    }
                }

                // Determine status
                ControlAuthorityStatus status;

                if (commandPods == 0)
                {
                    // No command pod = uncontrollable
                    status = ControlAuthorityStatus.None;
                }
                else if (reactionWheels == 0 && rcsCount == 0)
                {
                    // Command pod only, no attitude control
                    status = ControlAuthorityStatus.Marginal;
                }
                else
                {
                    // Has command pod and some form of attitude control
                    status = ControlAuthorityStatus.Good;
                }

                return new ControlAuthorityCheck(
                    status: status,
                    commandPodCount: commandPods,
                    reactionWheelCount: reactionWheels,
                    rcsCount: rcsCount
                );
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"ControlAuthorityCalculator.Calculate failed: {ex.Message}");
                return ControlAuthorityCheck.NotAvailable;
            }
        }
    }
}

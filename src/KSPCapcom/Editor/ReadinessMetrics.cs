using System;
using System.Collections.Generic;
using System.Text;

namespace KSPCapcom.Editor
{
    /// <summary>
    /// Container for all derived readiness metrics for a craft.
    /// Includes TWR, delta-V, control authority, and staging validation.
    /// </summary>
    public class ReadinessMetrics
    {
        public TWRAnalysis TWR { get; }
        public DeltaVEstimate DeltaV { get; }
        public ControlAuthorityCheck ControlAuthority { get; }
        public StagingValidation Staging { get; }

        public ReadinessMetrics(
            TWRAnalysis twr,
            DeltaVEstimate deltaV,
            ControlAuthorityCheck controlAuthority,
            StagingValidation staging)
        {
            TWR = twr ?? TWRAnalysis.NotAvailable;
            DeltaV = deltaV ?? DeltaVEstimate.NotAvailable;
            ControlAuthority = controlAuthority ?? ControlAuthorityCheck.NotAvailable;
            Staging = staging ?? StagingValidation.Empty;
        }

        /// <summary>
        /// Singleton for when metrics cannot be calculated.
        /// </summary>
        public static readonly ReadinessMetrics NotAvailable = new ReadinessMetrics(
            TWRAnalysis.NotAvailable,
            DeltaVEstimate.NotAvailable,
            ControlAuthorityCheck.NotAvailable,
            StagingValidation.Empty
        );

        /// <summary>
        /// Generate a human-readable summary for inclusion in LLM prompts.
        /// </summary>
        public string ToPromptSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("CRAFT READINESS (rough estimates):");
            sb.AppendLine(TWR.ToPromptLine());
            sb.AppendLine(DeltaV.ToPromptLine());
            sb.AppendLine(ControlAuthority.ToPromptLine());
            sb.Append(Staging.ToPromptLine());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Thrust-to-Weight Ratio analysis for the first stage (stage 0).
    /// </summary>
    public class TWRAnalysis
    {
        public float VacuumTWR { get; }
        public float AtmosphericTWR { get; }
        public int EngineCount { get; }
        public bool CanLaunchFromKerbin { get; }
        public bool IsAvailable { get; }

        public TWRAnalysis(float vacuumTWR, float atmosphericTWR, int engineCount, bool canLaunchFromKerbin)
        {
            VacuumTWR = vacuumTWR;
            AtmosphericTWR = atmosphericTWR;
            EngineCount = engineCount;
            CanLaunchFromKerbin = canLaunchFromKerbin;
            IsAvailable = true;
        }

        private TWRAnalysis()
        {
            IsAvailable = false;
        }

        public static readonly TWRAnalysis NotAvailable = new TWRAnalysis();

        public string ToPromptLine()
        {
            if (!IsAvailable)
                return "TWR: N/A";

            var warning = !CanLaunchFromKerbin ? " (WARN: TWR < 1.0 at sea level)" : "";
            return $"TWR: {AtmosphericTWR:F2} (ASL) / {VacuumTWR:F2} (vac) with {EngineCount} engines{warning}";
        }
    }

    /// <summary>
    /// Delta-V estimates for the craft.
    /// MVP: Single-stage total. Future: Per-stage breakdown.
    /// </summary>
    public class DeltaVEstimate
    {
        public float TotalDeltaV { get; }
        public IReadOnlyList<StageDeltaV> PerStage { get; }
        public bool IsAvailable { get; }

        public DeltaVEstimate(float totalDeltaV, IReadOnlyList<StageDeltaV> perStage = null)
        {
            TotalDeltaV = totalDeltaV;
            PerStage = perStage ?? new List<StageDeltaV>();
            IsAvailable = true;
        }

        private DeltaVEstimate()
        {
            TotalDeltaV = 0f;
            PerStage = new List<StageDeltaV>();
            IsAvailable = false;
        }

        public static readonly DeltaVEstimate NotAvailable = new DeltaVEstimate();

        public string ToPromptLine()
        {
            if (!IsAvailable)
                return "Delta-V (rough): N/A";

            return $"Delta-V (rough): Total ~{TotalDeltaV:F0}m/s";
        }
    }

    /// <summary>
    /// Delta-V for a single stage (future enhancement).
    /// </summary>
    public class StageDeltaV
    {
        public int StageNumber { get; }
        public float DeltaV { get; }
        public float WetMass { get; }
        public float DryMass { get; }

        public StageDeltaV(int stageNumber, float deltaV, float wetMass, float dryMass)
        {
            StageNumber = stageNumber;
            DeltaV = deltaV;
            WetMass = wetMass;
            DryMass = dryMass;
        }
    }

    /// <summary>
    /// Control authority check based on presence of command, reaction wheels, and RCS.
    /// </summary>
    public class ControlAuthorityCheck
    {
        public ControlAuthorityStatus Status { get; }
        public int CommandPodCount { get; }
        public int ReactionWheelCount { get; }
        public int RCSCount { get; }
        public bool IsAvailable { get; }

        public ControlAuthorityCheck(
            ControlAuthorityStatus status,
            int commandPodCount,
            int reactionWheelCount,
            int rcsCount)
        {
            Status = status;
            CommandPodCount = commandPodCount;
            ReactionWheelCount = reactionWheelCount;
            RCSCount = rcsCount;
            IsAvailable = true;
        }

        private ControlAuthorityCheck()
        {
            Status = ControlAuthorityStatus.None;
            IsAvailable = false;
        }

        public static readonly ControlAuthorityCheck NotAvailable = new ControlAuthorityCheck();

        public string ToPromptLine()
        {
            if (!IsAvailable)
                return "Control Authority: N/A";

            string statusText;
            switch (Status)
            {
                case ControlAuthorityStatus.None:
                    statusText = "WARN: No command pod";
                    break;
                case ControlAuthorityStatus.Marginal:
                    statusText = "MARGINAL: Command pod only, no attitude control";
                    break;
                case ControlAuthorityStatus.Good:
                    statusText = "OK";
                    break;
                default:
                    statusText = "Unknown";
                    break;
            }

            return $"Control Authority: {statusText}";
        }
    }

    /// <summary>
    /// Control authority status levels.
    /// </summary>
    public enum ControlAuthorityStatus
    {
        /// <summary>No command pod present.</summary>
        None,
        /// <summary>Command pod only, no reaction wheels or RCS.</summary>
        Marginal,
        /// <summary>Command pod with reaction wheels or RCS.</summary>
        Good
    }

    /// <summary>
    /// Validation results for staging configuration.
    /// </summary>
    public class StagingValidation
    {
        public IReadOnlyList<string> Warnings { get; }

        public StagingValidation(IReadOnlyList<string> warnings)
        {
            Warnings = warnings ?? new List<string>();
        }

        public static readonly StagingValidation Empty = new StagingValidation(new List<string>());

        public bool HasWarnings => Warnings.Count > 0;

        public string ToPromptLine()
        {
            if (!HasWarnings)
                return "Staging: OK";

            var sb = new StringBuilder();
            sb.Append("Staging: WARNINGS");
            foreach (var warning in Warnings)
            {
                sb.AppendLine();
                sb.Append($"  - {warning}");
            }
            return sb.ToString();
        }
    }
}

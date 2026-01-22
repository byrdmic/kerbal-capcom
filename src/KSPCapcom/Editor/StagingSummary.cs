using System.Collections.Generic;
using System.Text;

namespace KSPCapcom.Editor
{
    /// <summary>
    /// Summary of staging configuration for a craft.
    /// </summary>
    public class StagingSummary
    {
        public int StageCount { get; }
        public IReadOnlyList<StageInfo> Stages { get; }

        public StagingSummary(int stageCount, IReadOnlyList<StageInfo> stages)
        {
            StageCount = stageCount;
            Stages = stages ?? new List<StageInfo>();
        }

        /// <summary>
        /// Build a StagingSummary from a ShipConstruct.
        /// </summary>
        public static StagingSummary FromShip(ShipConstruct ship)
        {
            if (ship == null || ship.Parts == null || ship.Parts.Count == 0)
            {
                return new StagingSummary(0, new List<StageInfo>());
            }

            // Find the maximum stage number
            int maxStage = 0;
            foreach (var part in ship.Parts)
            {
                if (part.inverseStage > maxStage)
                {
                    maxStage = part.inverseStage;
                }
            }

            // Collect info for each stage
            var stages = new List<StageInfo>();
            for (int stageNum = 0; stageNum <= maxStage; stageNum++)
            {
                var stageInfo = StageInfo.FromParts(stageNum, ship.Parts);
                if (stageInfo.PartCount > 0)
                {
                    stages.Add(stageInfo);
                }
            }

            return new StagingSummary(stages.Count, stages);
        }

        /// <summary>
        /// Empty staging summary for empty crafts.
        /// </summary>
        public static readonly StagingSummary Empty = new StagingSummary(0, new List<StageInfo>());

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"stageCount\":{StageCount}");
            sb.Append(",\"stages\":[");
            for (int i = 0; i < Stages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(Stages[i].ToJson());
            }
            sb.Append("]");
            sb.Append("}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Information about a single stage in the staging sequence.
    /// </summary>
    public class StageInfo
    {
        public int StageNumber { get; }
        public int PartCount { get; }
        public bool HasDecoupler { get; }
        public bool HasEngine { get; }

        public StageInfo(int stageNumber, int partCount, bool hasDecoupler, bool hasEngine)
        {
            StageNumber = stageNumber;
            PartCount = partCount;
            HasDecoupler = hasDecoupler;
            HasEngine = hasEngine;
        }

        /// <summary>
        /// Create a StageInfo from parts in a specific stage.
        /// </summary>
        public static StageInfo FromParts(int stageNumber, IList<Part> allParts)
        {
            int partCount = 0;
            bool hasDecoupler = false;
            bool hasEngine = false;

            if (allParts != null)
            {
                foreach (var part in allParts)
                {
                    if (part.inverseStage != stageNumber)
                        continue;

                    partCount++;

                    // Check for engine modules
                    if (!hasEngine && PartCategorizer.HasEngineModule(part))
                    {
                        hasEngine = true;
                    }

                    // Check for decoupler modules
                    if (!hasDecoupler && PartCategorizer.HasDecouplerModule(part))
                    {
                        hasDecoupler = true;
                    }
                }
            }

            return new StageInfo(stageNumber, partCount, hasDecoupler, hasEngine);
        }

        public string ToJson()
        {
            return $"{{\"stageNumber\":{StageNumber},\"partCount\":{PartCount},\"hasDecoupler\":{(HasDecoupler ? "true" : "false")},\"hasEngine\":{(HasEngine ? "true" : "false")}}}";
        }
    }
}

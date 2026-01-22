using System;
using System.Collections.Generic;
using System.Text;
using KSPCapcom.Editor.Calculators;

namespace KSPCapcom.Editor
{
    /// <summary>
    /// Lightweight snapshot of a craft in the VAB/SPH editor.
    /// Captures parts, resources, and staging metadata for LLM context.
    /// </summary>
    public class EditorCraftSnapshot
    {
        public string CraftName { get; }
        public string Facility { get; }
        public int TotalPartCount { get; }
        public float TotalMass { get; }
        public float TotalDryMass { get; }

        public IReadOnlyList<EngineSummary> Engines { get; }
        public IReadOnlyList<FuelTankSummary> FuelTanks { get; }
        public IReadOnlyList<ControlSummary> ControlParts { get; }
        public IReadOnlyList<DecouplerSummary> Decouplers { get; }
        public IReadOnlyList<ResourceSummary> Resources { get; }
        public StagingSummary Staging { get; }

        private ReadinessMetrics _cachedMetrics;

        /// <summary>
        /// Derived readiness metrics calculated on-demand.
        /// Includes TWR, delta-V, control authority, and staging validation.
        /// </summary>
        public ReadinessMetrics Readiness
        {
            get
            {
                if (_cachedMetrics == null)
                {
                    _cachedMetrics = ReadinessCalculator.Calculate(this);
                }
                return _cachedMetrics;
            }
        }

        /// <summary>
        /// Whether this snapshot represents an empty or invalid craft.
        /// </summary>
        public bool IsEmpty => TotalPartCount == 0;

        /// <summary>
        /// Empty snapshot singleton for use when no craft is available.
        /// </summary>
        public static readonly EditorCraftSnapshot Empty = new EditorCraftSnapshot(
            craftName: "",
            facility: "",
            totalPartCount: 0,
            totalMass: 0f,
            totalDryMass: 0f,
            engines: new List<EngineSummary>(),
            fuelTanks: new List<FuelTankSummary>(),
            controlParts: new List<ControlSummary>(),
            decouplers: new List<DecouplerSummary>(),
            resources: new List<ResourceSummary>(),
            staging: StagingSummary.Empty
        );

        private EditorCraftSnapshot(
            string craftName,
            string facility,
            int totalPartCount,
            float totalMass,
            float totalDryMass,
            IReadOnlyList<EngineSummary> engines,
            IReadOnlyList<FuelTankSummary> fuelTanks,
            IReadOnlyList<ControlSummary> controlParts,
            IReadOnlyList<DecouplerSummary> decouplers,
            IReadOnlyList<ResourceSummary> resources,
            StagingSummary staging)
        {
            CraftName = craftName ?? "";
            Facility = facility ?? "";
            TotalPartCount = totalPartCount;
            TotalMass = totalMass;
            TotalDryMass = totalDryMass;
            Engines = engines ?? new List<EngineSummary>();
            FuelTanks = fuelTanks ?? new List<FuelTankSummary>();
            ControlParts = controlParts ?? new List<ControlSummary>();
            Decouplers = decouplers ?? new List<DecouplerSummary>();
            Resources = resources ?? new List<ResourceSummary>();
            Staging = staging ?? StagingSummary.Empty;
        }

        /// <summary>
        /// Capture a snapshot of the current craft in the editor.
        /// </summary>
        /// <param name="ship">The ShipConstruct from EditorLogic.fetch.ship</param>
        /// <returns>A snapshot of the craft, or Empty if the ship is null/invalid.</returns>
        public static EditorCraftSnapshot Capture(ShipConstruct ship)
        {
            if (ship == null || ship.Parts == null || ship.Parts.Count == 0)
            {
                return Empty;
            }

            try
            {
                return CaptureInternal(ship);
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"EditorCraftSnapshot.Capture failed: {ex.Message}");
                return Empty;
            }
        }

        private static EditorCraftSnapshot CaptureInternal(ShipConstruct ship)
        {
            // Determine facility
            string facility = "";
            if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                facility = EditorDriver.editorFacility == EditorFacility.VAB ? "VAB" : "SPH";
            }

            // Calculate masses
            float totalMass = 0f;
            float totalDryMass = 0f;

            foreach (var part in ship.Parts)
            {
                if (part == null)
                    continue;

                try
                {
                    totalMass += part.mass + part.GetResourceMass();
                    totalDryMass += part.mass;
                }
                catch
                {
                    // Skip parts that throw during mass calculation
                }
            }

            // Gather part summaries
            var engines = PartCategorizer.GetEngines(ship.Parts);
            var fuelTanks = PartCategorizer.GetFuelTanks(ship.Parts);
            var controlParts = PartCategorizer.GetControlParts(ship.Parts);
            var decouplers = PartCategorizer.GetDecouplers(ship.Parts);
            var resources = ResourceSummary.Aggregate(ship.Parts);
            var staging = StagingSummary.FromShip(ship);

            return new EditorCraftSnapshot(
                craftName: ship.shipName ?? "Untitled",
                facility: facility,
                totalPartCount: ship.Parts.Count,
                totalMass: totalMass,
                totalDryMass: totalDryMass,
                engines: engines,
                fuelTanks: fuelTanks,
                controlParts: controlParts,
                decouplers: decouplers,
                resources: resources,
                staging: staging
            );
        }

        /// <summary>
        /// Serialize the snapshot to JSON.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");

            sb.Append($"\"craftName\":\"{JsonEscape(CraftName)}\"");
            sb.Append($",\"facility\":\"{JsonEscape(Facility)}\"");
            sb.Append($",\"totalPartCount\":{TotalPartCount}");
            sb.Append($",\"totalMass\":{TotalMass.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.Append($",\"totalDryMass\":{TotalDryMass.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            // Engines array
            sb.Append(",\"engines\":[");
            for (int i = 0; i < Engines.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(Engines[i].ToJson());
            }
            sb.Append("]");

            // Fuel tanks array
            sb.Append(",\"fuelTanks\":[");
            for (int i = 0; i < FuelTanks.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(FuelTanks[i].ToJson());
            }
            sb.Append("]");

            // Control parts array
            sb.Append(",\"controlParts\":[");
            for (int i = 0; i < ControlParts.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(ControlParts[i].ToJson());
            }
            sb.Append("]");

            // Decouplers array
            sb.Append(",\"decouplers\":[");
            for (int i = 0; i < Decouplers.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(Decouplers[i].ToJson());
            }
            sb.Append("]");

            // Resources array
            sb.Append(",\"resources\":[");
            for (int i = 0; i < Resources.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(Resources[i].ToJson());
            }
            sb.Append("]");

            // Staging
            sb.Append(",\"staging\":");
            sb.Append(Staging.ToJson());

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Generate a human-readable summary suitable for inclusion in LLM prompts.
        /// </summary>
        public string ToPromptSummary()
        {
            if (IsEmpty)
            {
                return "";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"CURRENT CRAFT: {CraftName}");
            sb.AppendLine($"Editor: {Facility}");
            sb.AppendLine($"Parts: {TotalPartCount}, Mass: {TotalMass:F1}t (dry: {TotalDryMass:F1}t)");

            // Engines summary
            if (Engines.Count > 0)
            {
                float totalThrust = 0f;
                foreach (var engine in Engines)
                {
                    totalThrust += engine.MaxThrust;
                }
                sb.AppendLine($"Engines: {Engines.Count} (total thrust: {totalThrust:F0}kN)");
            }

            // Resources summary
            if (Resources.Count > 0)
            {
                sb.Append("Resources: ");
                var resourceParts = new List<string>();
                foreach (var resource in Resources)
                {
                    resourceParts.Add($"{resource.ResourceName}: {resource.TotalAmount:F0}/{resource.TotalCapacity:F0}");
                }
                sb.AppendLine(string.Join(", ", resourceParts));
            }

            // Staging summary
            if (Staging.StageCount > 0)
            {
                sb.AppendLine($"Staging: {Staging.StageCount} stages");
            }

            // Control summary
            int commandPods = 0;
            int reactionWheels = 0;
            int rcsParts = 0;
            foreach (var control in ControlParts)
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
                        rcsParts++;
                        break;
                }
            }

            if (commandPods > 0 || reactionWheels > 0 || rcsParts > 0)
            {
                var controlParts = new List<string>();
                if (commandPods > 0) controlParts.Add($"{commandPods} command");
                if (reactionWheels > 0) controlParts.Add($"{reactionWheels} reaction wheel");
                if (rcsParts > 0) controlParts.Add($"{rcsParts} RCS");
                sb.AppendLine($"Control: {string.Join(", ", controlParts)}");
            }

            // Add readiness metrics
            sb.Append(Readiness.ToPromptSummary());

            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KSPCapcom.Editor
{
    /// <summary>
    /// Summary of an engine part including thrust and ISP values.
    /// </summary>
    public class EngineSummary
    {
        public string PartName { get; }
        public float MaxThrust { get; }
        public float VacuumIsp { get; }
        public float AtmosphericIsp { get; }
        public IReadOnlyList<string> Propellants { get; }
        public bool IsThrottleLocked { get; }

        public EngineSummary(
            string partName,
            float maxThrust,
            float vacuumIsp,
            float atmosphericIsp,
            IReadOnlyList<string> propellants,
            bool isThrottleLocked)
        {
            PartName = partName ?? "";
            MaxThrust = maxThrust;
            VacuumIsp = vacuumIsp;
            AtmosphericIsp = atmosphericIsp;
            Propellants = propellants ?? new List<string>();
            IsThrottleLocked = isThrottleLocked;
        }

        /// <summary>
        /// Create an EngineSummary from a ModuleEngines instance.
        /// </summary>
        public static EngineSummary FromModule(Part part, ModuleEngines engine)
        {
            if (part == null || engine == null)
                return null;

            var propellantNames = new List<string>();
            if (engine.propellants != null)
            {
                foreach (var prop in engine.propellants)
                {
                    if (!string.IsNullOrEmpty(prop.name))
                    {
                        propellantNames.Add(prop.name);
                    }
                }
            }

            // Get ISP values from atmosphere curve
            float vacuumIsp = 0f;
            float atmIsp = 0f;

            if (engine.atmosphereCurve != null)
            {
                vacuumIsp = engine.atmosphereCurve.Evaluate(0f);
                atmIsp = engine.atmosphereCurve.Evaluate(1f);
            }

            return new EngineSummary(
                partName: part.partInfo?.title ?? part.name,
                maxThrust: engine.maxThrust,
                vacuumIsp: vacuumIsp,
                atmosphericIsp: atmIsp,
                propellants: propellantNames,
                isThrottleLocked: engine.throttleLocked
            );
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"partName\":\"{JsonEscape(PartName)}\"");
            sb.Append($",\"maxThrust\":{MaxThrust.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.Append($",\"vacuumIsp\":{VacuumIsp.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.Append($",\"atmosphericIsp\":{AtmosphericIsp.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.Append(",\"propellants\":[");
            for (int i = 0; i < Propellants.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"\"{JsonEscape(Propellants[i])}\"");
            }
            sb.Append("]");
            sb.Append($",\"isThrottleLocked\":{(IsThrottleLocked ? "true" : "false")}");
            sb.Append("}");
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

    /// <summary>
    /// Summary of a fuel tank part with resource capacities.
    /// </summary>
    public class FuelTankSummary
    {
        public string PartName { get; }
        public IReadOnlyList<ResourceCapacity> ResourceCapacities { get; }

        public FuelTankSummary(string partName, IReadOnlyList<ResourceCapacity> resourceCapacities)
        {
            PartName = partName ?? "";
            ResourceCapacities = resourceCapacities ?? new List<ResourceCapacity>();
        }

        /// <summary>
        /// Create a FuelTankSummary from a Part with fuel resources.
        /// </summary>
        public static FuelTankSummary FromPart(Part part, IReadOnlyList<string> fuelResourceNames)
        {
            if (part == null)
                return null;

            var capacities = new List<ResourceCapacity>();

            if (part.Resources != null && fuelResourceNames != null)
            {
                foreach (PartResource resource in part.Resources)
                {
                    if (fuelResourceNames.Contains(resource.resourceName))
                    {
                        capacities.Add(new ResourceCapacity(
                            resource.resourceName,
                            resource.amount,
                            resource.maxAmount
                        ));
                    }
                }
            }

            if (capacities.Count == 0)
                return null;

            return new FuelTankSummary(
                partName: part.partInfo?.title ?? part.name,
                resourceCapacities: capacities
            );
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"partName\":\"{JsonEscape(PartName)}\"");
            sb.Append(",\"resourceCapacities\":[");
            for (int i = 0; i < ResourceCapacities.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(ResourceCapacities[i].ToJson());
            }
            sb.Append("]");
            sb.Append("}");
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

    /// <summary>
    /// Represents the capacity of a single resource type.
    /// </summary>
    public class ResourceCapacity
    {
        public string ResourceName { get; }
        public double Amount { get; }
        public double MaxAmount { get; }

        public ResourceCapacity(string resourceName, double amount, double maxAmount)
        {
            ResourceName = resourceName ?? "";
            Amount = amount;
            MaxAmount = maxAmount;
        }

        public string ToJson()
        {
            return $"{{\"resourceName\":\"{JsonEscape(ResourceName)}\",\"amount\":{Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"maxAmount\":{MaxAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
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

    /// <summary>
    /// Summary of a control part (reaction wheel, SAS, RCS, or command pod).
    /// </summary>
    public class ControlSummary
    {
        public string PartName { get; }
        public ControlType Type { get; }
        public float Torque { get; }
        public float RcsThrust { get; }

        public ControlSummary(string partName, ControlType type, float torque, float rcsThrust)
        {
            PartName = partName ?? "";
            Type = type;
            Torque = torque;
            RcsThrust = rcsThrust;
        }

        /// <summary>
        /// Create a ControlSummary from a part with a reaction wheel module.
        /// </summary>
        public static ControlSummary FromReactionWheel(Part part, ModuleReactionWheel wheel)
        {
            if (part == null || wheel == null)
                return null;

            // Average of pitch, yaw, roll torque
            float avgTorque = (wheel.PitchTorque + wheel.YawTorque + wheel.RollTorque) / 3f;

            return new ControlSummary(
                partName: part.partInfo?.title ?? part.name,
                type: ControlType.ReactionWheel,
                torque: avgTorque,
                rcsThrust: 0f
            );
        }

        /// <summary>
        /// Create a ControlSummary from a part with a command module.
        /// </summary>
        public static ControlSummary FromCommandModule(Part part, ModuleCommand command)
        {
            if (part == null || command == null)
                return null;

            return new ControlSummary(
                partName: part.partInfo?.title ?? part.name,
                type: ControlType.CommandPod,
                torque: 0f,
                rcsThrust: 0f
            );
        }

        /// <summary>
        /// Create a ControlSummary from a part with an RCS module.
        /// </summary>
        public static ControlSummary FromRCS(Part part, ModuleRCS rcs)
        {
            if (part == null || rcs == null)
                return null;

            return new ControlSummary(
                partName: part.partInfo?.title ?? part.name,
                type: ControlType.RCS,
                torque: 0f,
                rcsThrust: rcs.thrusterPower
            );
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"partName\":\"{JsonEscape(PartName)}\"");
            sb.Append($",\"type\":\"{Type}\"");

            if (Torque > 0)
            {
                sb.Append($",\"torque\":{Torque.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            if (RcsThrust > 0)
            {
                sb.Append($",\"rcsThrust\":{RcsThrust.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            sb.Append("}");
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

    /// <summary>
    /// Types of control systems available on a craft.
    /// </summary>
    public enum ControlType
    {
        ReactionWheel,
        SAS,
        RCS,
        CommandPod
    }

    /// <summary>
    /// Summary of a decoupler part.
    /// </summary>
    public class DecouplerSummary
    {
        public string PartName { get; }
        public float EjectionForce { get; }
        public bool IsRadial { get; }

        public DecouplerSummary(string partName, float ejectionForce, bool isRadial)
        {
            PartName = partName ?? "";
            EjectionForce = ejectionForce;
            IsRadial = isRadial;
        }

        /// <summary>
        /// Create a DecouplerSummary from a ModuleDecouple.
        /// </summary>
        public static DecouplerSummary FromModuleDecouple(Part part, ModuleDecouple decouple)
        {
            if (part == null || decouple == null)
                return null;

            return new DecouplerSummary(
                partName: part.partInfo?.title ?? part.name,
                ejectionForce: decouple.ejectionForce,
                isRadial: false
            );
        }

        /// <summary>
        /// Create a DecouplerSummary from a ModuleAnchoredDecoupler (radial decouplers).
        /// </summary>
        public static DecouplerSummary FromAnchoredDecoupler(Part part, ModuleAnchoredDecoupler decouple)
        {
            if (part == null || decouple == null)
                return null;

            return new DecouplerSummary(
                partName: part.partInfo?.title ?? part.name,
                ejectionForce: decouple.ejectionForce,
                isRadial: true
            );
        }

        public string ToJson()
        {
            return $"{{\"partName\":\"{JsonEscape(PartName)}\",\"ejectionForce\":{EjectionForce.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"isRadial\":{(IsRadial ? "true" : "false")}}}";
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

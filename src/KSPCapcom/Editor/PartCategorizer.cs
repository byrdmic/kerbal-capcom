using System.Collections.Generic;
using System.Linq;

namespace KSPCapcom.Editor
{
    /// <summary>
    /// Static utility class for categorizing parts by their PartModules.
    /// </summary>
    public static class PartCategorizer
    {
        /// <summary>
        /// Common fuel resource names used to identify fuel tanks.
        /// </summary>
        public static readonly IReadOnlyList<string> FuelResourceNames = new List<string>
        {
            "LiquidFuel",
            "Oxidizer",
            "MonoPropellant",
            "SolidFuel",
            "XenonGas",
            "IntakeAir"
        };

        /// <summary>
        /// Check if a part has an engine module (ModuleEngines or ModuleEnginesFX).
        /// </summary>
        public static bool HasEngineModule(Part part)
        {
            if (part?.Modules == null)
                return false;

            foreach (PartModule module in part.Modules)
            {
                if (module is ModuleEngines || module is ModuleEnginesFX)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a part has a decoupler module.
        /// </summary>
        public static bool HasDecouplerModule(Part part)
        {
            if (part?.Modules == null)
                return false;

            foreach (PartModule module in part.Modules)
            {
                if (module is ModuleDecouple || module is ModuleAnchoredDecoupler)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a part has a reaction wheel module.
        /// </summary>
        public static bool HasReactionWheelModule(Part part)
        {
            if (part?.Modules == null)
                return false;

            foreach (PartModule module in part.Modules)
            {
                if (module is ModuleReactionWheel)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a part has a command module.
        /// </summary>
        public static bool HasCommandModule(Part part)
        {
            if (part?.Modules == null)
                return false;

            foreach (PartModule module in part.Modules)
            {
                if (module is ModuleCommand)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a part has an RCS module.
        /// </summary>
        public static bool HasRCSModule(Part part)
        {
            if (part?.Modules == null)
                return false;

            foreach (PartModule module in part.Modules)
            {
                if (module is ModuleRCS || module is ModuleRCSFX)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a part is a fuel tank (has fuel resources but no engine module).
        /// </summary>
        public static bool IsFuelTank(Part part)
        {
            if (part?.Resources == null)
                return false;

            // Must not be an engine
            if (HasEngineModule(part))
                return false;

            // Must have at least one fuel resource
            foreach (PartResource resource in part.Resources)
            {
                if (FuelResourceNames.Contains(resource.resourceName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get all engine summaries from a collection of parts.
        /// </summary>
        public static IReadOnlyList<EngineSummary> GetEngines(IEnumerable<Part> parts)
        {
            var engines = new List<EngineSummary>();

            if (parts == null)
                return engines;

            foreach (var part in parts)
            {
                if (part?.Modules == null)
                    continue;

                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleEngines engine)
                    {
                        var summary = EngineSummary.FromModule(part, engine);
                        if (summary != null)
                        {
                            engines.Add(summary);
                        }
                        break; // Only get one engine per part
                    }
                }
            }

            return engines;
        }

        /// <summary>
        /// Get all fuel tank summaries from a collection of parts.
        /// </summary>
        public static IReadOnlyList<FuelTankSummary> GetFuelTanks(IEnumerable<Part> parts)
        {
            var tanks = new List<FuelTankSummary>();

            if (parts == null)
                return tanks;

            foreach (var part in parts)
            {
                if (!IsFuelTank(part))
                    continue;

                var summary = FuelTankSummary.FromPart(part, FuelResourceNames);
                if (summary != null)
                {
                    tanks.Add(summary);
                }
            }

            return tanks;
        }

        /// <summary>
        /// Get all control part summaries from a collection of parts.
        /// </summary>
        public static IReadOnlyList<ControlSummary> GetControlParts(IEnumerable<Part> parts)
        {
            var controls = new List<ControlSummary>();

            if (parts == null)
                return controls;

            foreach (var part in parts)
            {
                if (part?.Modules == null)
                    continue;

                // Check for command module first
                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleCommand command)
                    {
                        var summary = ControlSummary.FromCommandModule(part, command);
                        if (summary != null)
                        {
                            controls.Add(summary);
                        }
                        break; // Only one control type per part for command modules
                    }
                }

                // Check for reaction wheels
                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleReactionWheel wheel)
                    {
                        var summary = ControlSummary.FromReactionWheel(part, wheel);
                        if (summary != null)
                        {
                            controls.Add(summary);
                        }
                        break;
                    }
                }

                // Check for RCS
                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleRCS rcs)
                    {
                        var summary = ControlSummary.FromRCS(part, rcs);
                        if (summary != null)
                        {
                            controls.Add(summary);
                        }
                        break;
                    }
                }
            }

            return controls;
        }

        /// <summary>
        /// Get all decoupler summaries from a collection of parts.
        /// </summary>
        public static IReadOnlyList<DecouplerSummary> GetDecouplers(IEnumerable<Part> parts)
        {
            var decouplers = new List<DecouplerSummary>();

            if (parts == null)
                return decouplers;

            foreach (var part in parts)
            {
                if (part?.Modules == null)
                    continue;

                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleAnchoredDecoupler anchoredDecouple)
                    {
                        var summary = DecouplerSummary.FromAnchoredDecoupler(part, anchoredDecouple);
                        if (summary != null)
                        {
                            decouplers.Add(summary);
                        }
                        break;
                    }
                    else if (module is ModuleDecouple decouple)
                    {
                        var summary = DecouplerSummary.FromModuleDecouple(part, decouple);
                        if (summary != null)
                        {
                            decouplers.Add(summary);
                        }
                        break;
                    }
                }
            }

            return decouplers;
        }
    }
}

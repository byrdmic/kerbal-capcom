using System.Collections.Generic;
using System.Text;

namespace KSPCapcom.Editor
{
    /// <summary>
    /// Aggregated totals for a single resource type across all parts.
    /// </summary>
    public class ResourceSummary
    {
        public string ResourceName { get; }
        public double TotalAmount { get; }
        public double TotalCapacity { get; }

        public ResourceSummary(string resourceName, double totalAmount, double totalCapacity)
        {
            ResourceName = resourceName ?? "";
            TotalAmount = totalAmount;
            TotalCapacity = totalCapacity;
        }

        /// <summary>
        /// Aggregate all resources from a collection of parts into resource summaries.
        /// </summary>
        /// <param name="parts">The parts to aggregate resources from.</param>
        /// <returns>A list of resource summaries, one per unique resource type.</returns>
        public static IReadOnlyList<ResourceSummary> Aggregate(IEnumerable<Part> parts)
        {
            if (parts == null)
                return new List<ResourceSummary>();

            var totals = new Dictionary<string, (double amount, double capacity)>();

            foreach (var part in parts)
            {
                if (part?.Resources == null)
                    continue;

                foreach (PartResource resource in part.Resources)
                {
                    if (string.IsNullOrEmpty(resource.resourceName))
                        continue;

                    if (totals.TryGetValue(resource.resourceName, out var existing))
                    {
                        totals[resource.resourceName] = (
                            existing.amount + resource.amount,
                            existing.capacity + resource.maxAmount
                        );
                    }
                    else
                    {
                        totals[resource.resourceName] = (resource.amount, resource.maxAmount);
                    }
                }
            }

            var result = new List<ResourceSummary>();
            foreach (var kvp in totals)
            {
                result.Add(new ResourceSummary(kvp.Key, kvp.Value.amount, kvp.Value.capacity));
            }

            // Sort by resource name for consistent ordering
            result.Sort((a, b) => string.Compare(a.ResourceName, b.ResourceName, System.StringComparison.Ordinal));

            return result;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"resourceName\":\"{JsonEscape(ResourceName)}\"");
            sb.Append($",\"totalAmount\":{TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.Append($",\"totalCapacity\":{TotalCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
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
}

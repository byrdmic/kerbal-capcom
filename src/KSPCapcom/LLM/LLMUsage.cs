namespace KSPCapcom.LLM
{
    /// <summary>
    /// Token usage statistics from an LLM response.
    /// All values are optional as not all providers report usage.
    /// </summary>
    public class LLMUsage
    {
        /// <summary>
        /// Number of tokens in the input/prompt.
        /// Null if not reported by the provider.
        /// </summary>
        public int? InputTokens { get; }

        /// <summary>
        /// Number of tokens in the generated output.
        /// Null if not reported by the provider.
        /// </summary>
        public int? OutputTokens { get; }

        /// <summary>
        /// Total tokens used (input + output).
        /// Null if not reported by the provider.
        /// </summary>
        public int? TotalTokens { get; }

        /// <summary>
        /// Whether any usage data was reported.
        /// </summary>
        public bool HasData =>
            InputTokens.HasValue || OutputTokens.HasValue || TotalTokens.HasValue;

        public LLMUsage(int? inputTokens = null, int? outputTokens = null, int? totalTokens = null)
        {
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
            TotalTokens = totalTokens;
        }

        /// <summary>
        /// Empty usage instance for when no data is available.
        /// </summary>
        public static LLMUsage Empty { get; } = new LLMUsage();

        /// <summary>
        /// Create usage from input and output counts, calculating total.
        /// </summary>
        public static LLMUsage FromCounts(int inputTokens, int outputTokens)
        {
            return new LLMUsage(inputTokens, outputTokens, inputTokens + outputTokens);
        }

        public override string ToString()
        {
            if (!HasData)
            {
                return "Usage: (not reported)";
            }

            return $"Usage: {InputTokens ?? 0} in, {OutputTokens ?? 0} out, {TotalTokens ?? 0} total";
        }
    }
}

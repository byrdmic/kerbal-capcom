using System.Collections.Generic;
using KSPCapcom.LLM.OpenAI;

namespace KSPCapcom.LLM
{
    /// <summary>
    /// Configuration options for an LLM request.
    /// All properties have sensible defaults for typical usage.
    /// </summary>
    public class LLMRequestOptions
    {
        /// <summary>
        /// Default temperature for response generation.
        /// </summary>
        public const float DefaultTemperature = 1.0f;

        /// <summary>
        /// Default maximum tokens in the response.
        /// </summary>
        public const int DefaultMaxTokens = 1024;

        /// <summary>
        /// Default timeout in milliseconds.
        /// </summary>
        public const int DefaultTimeoutMs = 30000;

        /// <summary>
        /// Sampling temperature (0.0 = deterministic, 1.0+ = creative).
        /// </summary>
        public float Temperature { get; set; } = DefaultTemperature;

        /// <summary>
        /// Maximum number of tokens to generate in the response.
        /// </summary>
        public int MaxTokens { get; set; } = DefaultMaxTokens;

        /// <summary>
        /// Model identifier to use. If null, connector uses its default.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// System prompt to prepend to the conversation.
        /// If null, connector may use a default or none.
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = DefaultTimeoutMs;

        /// <summary>
        /// Whether to enable streaming responses (if supported by the connector).
        /// </summary>
        public bool EnableStreaming { get; set; } = true;

        /// <summary>
        /// Tools available for the model to call.
        /// </summary>
        public List<ToolDefinition> Tools { get; set; }

        /// <summary>
        /// Tool choice mode: "auto", "none", or a specific tool name.
        /// </summary>
        public string ToolChoice { get; set; }

        /// <summary>
        /// Create options with default values.
        /// </summary>
        public LLMRequestOptions()
        {
        }

        /// <summary>
        /// Create options with specified temperature and max tokens.
        /// </summary>
        public LLMRequestOptions(float temperature, int maxTokens)
        {
            Temperature = temperature;
            MaxTokens = maxTokens;
        }

        /// <summary>
        /// Default options instance for common usage.
        /// </summary>
        public static LLMRequestOptions Default => new LLMRequestOptions();

        /// <summary>
        /// Create a copy of these options.
        /// </summary>
        public LLMRequestOptions Clone()
        {
            return new LLMRequestOptions
            {
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                Model = Model,
                SystemPrompt = SystemPrompt,
                TimeoutMs = TimeoutMs,
                EnableStreaming = EnableStreaming,
                Tools = Tools != null ? new List<ToolDefinition>(Tools) : null,
                ToolChoice = ToolChoice
            };
        }
    }
}

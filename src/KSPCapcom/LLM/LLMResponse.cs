using System.Collections.Generic;
using KSPCapcom.LLM.OpenAI;

namespace KSPCapcom.LLM
{
    /// <summary>
    /// Response container for LLM requests.
    /// Encapsulates success/failure, content, errors, and usage statistics.
    /// </summary>
    public class LLMResponse
    {
        /// <summary>
        /// The generated text content. Empty string on failure.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Whether the request completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Error information if the request failed.
        /// </summary>
        public LLMError Error { get; }

        /// <summary>
        /// Token usage statistics, if available.
        /// </summary>
        public LLMUsage Usage { get; }

        /// <summary>
        /// The model that generated the response, if reported.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Tool calls requested by the model (if finish_reason is "tool_calls").
        /// </summary>
        public IReadOnlyList<ToolCall> ToolCalls { get; }

        /// <summary>
        /// Whether this response represents a cancelled request.
        /// </summary>
        public bool IsCancelled => Error?.Type == LLMErrorType.Cancelled;

        /// <summary>
        /// Whether this response requests tool calls.
        /// </summary>
        public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;

        private LLMResponse(
            string content,
            bool success,
            LLMError error,
            LLMUsage usage,
            string model,
            IReadOnlyList<ToolCall> toolCalls = null)
        {
            Content = content ?? string.Empty;
            Success = success;
            Error = error ?? LLMError.None;
            Usage = usage ?? LLMUsage.Empty;
            Model = model;
            ToolCalls = toolCalls;
        }

        /// <summary>
        /// Create a successful response with content.
        /// </summary>
        public static LLMResponse Ok(string content, LLMUsage usage = null, string model = null)
        {
            return new LLMResponse(
                content: content,
                success: true,
                error: LLMError.None,
                usage: usage,
                model: model);
        }

        /// <summary>
        /// Create a successful response with tool calls.
        /// </summary>
        public static LLMResponse WithToolCalls(
            IReadOnlyList<ToolCall> toolCalls,
            string content = null,
            LLMUsage usage = null,
            string model = null)
        {
            return new LLMResponse(
                content: content,
                success: true,
                error: LLMError.None,
                usage: usage,
                model: model,
                toolCalls: toolCalls);
        }

        /// <summary>
        /// Create a failed response with error details.
        /// </summary>
        public static LLMResponse Fail(LLMError error)
        {
            return new LLMResponse(
                content: string.Empty,
                success: false,
                error: error,
                usage: null,
                model: null);
        }

        /// <summary>
        /// Create a failed response with error type and message.
        /// </summary>
        public static LLMResponse Fail(LLMErrorType type, string message)
        {
            return Fail(new LLMError(type, message));
        }

        /// <summary>
        /// Create a cancelled response.
        /// </summary>
        public static LLMResponse Cancelled(string message = "Request was cancelled")
        {
            return Fail(LLMError.Cancelled(message));
        }

        /// <summary>
        /// Create a not configured response.
        /// </summary>
        public static LLMResponse NotConfigured(string message = "Connector not configured")
        {
            return Fail(LLMError.NotConfigured(message));
        }

        public override string ToString()
        {
            if (Success)
            {
                if (HasToolCalls)
                {
                    return $"OK: {ToolCalls.Count} tool call(s)";
                }

                var preview = Content.Length > 50
                    ? Content.Substring(0, 47) + "..."
                    : Content;
                return $"OK: \"{preview}\"";
            }

            return $"Error: {Error}";
        }
    }
}

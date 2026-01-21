using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KSPCapcom.LLM
{
    /// <summary>
    /// Interface for LLM backend connectors.
    /// Enables pluggable LLM providers without coupling the UI to any specific implementation.
    ///
    /// Cancellation Contract: Implementations must return LLMResponse.Cancelled() instead of
    /// throwing OperationCanceledException. This ensures consistent handling without try/catch.
    /// </summary>
    public interface ILLMConnector
    {
        /// <summary>
        /// Human-readable name of this connector (e.g., "OpenAI", "Claude", "Local Ollama").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether this connector is properly configured and ready to accept requests.
        /// Check this before calling SendChatAsync to provide clear "not configured" feedback.
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Send a chat completion request to the LLM.
        /// </summary>
        /// <param name="messages">The conversation history to send.</param>
        /// <param name="options">Request configuration options.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>
        /// The LLM response. On cancellation, returns LLMResponse.Cancelled() instead of throwing.
        /// </returns>
        Task<LLMResponse> SendChatAsync(
            IReadOnlyList<LLMMessage> messages,
            LLMRequestOptions options,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Extended interface for connectors that support streaming responses.
    /// Inherit from ILLMConnector to maintain backward compatibility.
    /// </summary>
    public interface ILLMStreamingConnector : ILLMConnector
    {
        /// <summary>
        /// Whether this connector supports streaming responses.
        /// Some connectors may implement this interface but conditionally disable streaming.
        /// </summary>
        bool SupportsStreaming { get; }

        /// <summary>
        /// Send a chat completion request with streaming response.
        /// </summary>
        /// <param name="messages">The conversation history to send.</param>
        /// <param name="options">Request configuration options.</param>
        /// <param name="onChunk">Callback invoked for each text chunk received.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <returns>
        /// The complete LLM response after streaming finishes.
        /// On cancellation, returns LLMResponse.Cancelled() instead of throwing.
        /// </returns>
        Task<LLMResponse> SendChatStreamingAsync(
            IReadOnlyList<LLMMessage> messages,
            LLMRequestOptions options,
            Action<string> onChunk,
            CancellationToken cancellationToken);
    }
}

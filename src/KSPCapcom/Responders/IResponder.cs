using System;
using System.Collections.Generic;
using System.Threading;
using KSPCapcom.LLM;
using KSPCapcom.Validation;

namespace KSPCapcom.Responders
{
    /// <summary>
    /// Represents a response from a responder.
    /// </summary>
    public class ResponderResult
    {
        /// <summary>The response text to display.</summary>
        public string Text { get; }

        /// <summary>Whether the response was successful.</summary>
        public bool Success { get; }

        /// <summary>Error message if Success is false.</summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Structured error information for detailed error rendering.
        /// Null for successful responses or legacy error paths.
        /// </summary>
        public LLMError Error { get; }

        /// <summary>
        /// Validation result for kOS identifiers in the response.
        /// Null if validation was not performed.
        /// </summary>
        public KosValidationResult ValidationResult { get; }

        /// <summary>
        /// Whether the response has validation warnings (unverified identifiers).
        /// </summary>
        public bool HasValidationWarnings => ValidationResult?.HasUnverifiedIdentifiers ?? false;

        private ResponderResult(string text, bool success, string errorMessage, KosValidationResult validationResult = null, LLMError error = null)
        {
            Text = text;
            Success = success;
            ErrorMessage = errorMessage;
            ValidationResult = validationResult;
            Error = error;
        }

        public static ResponderResult Ok(string text) =>
            new ResponderResult(text, true, null, null);

        public static ResponderResult Ok(string text, KosValidationResult validationResult) =>
            new ResponderResult(text, true, null, validationResult);

        public static ResponderResult Fail(string errorMessage) =>
            new ResponderResult(null, false, errorMessage, null, null);

        public static ResponderResult Fail(string errorMessage, LLMError error) =>
            new ResponderResult(null, false, errorMessage, null, error);
    }

    /// <summary>
    /// Interface for message responders.
    /// Implementations handle generating responses to user messages.
    /// </summary>
    public interface IResponder
    {
        /// <summary>
        /// Generate a response to the user's message.
        /// </summary>
        /// <param name="userMessage">The user's input text.</param>
        /// <param name="conversationHistory">Previous messages for context.</param>
        /// <param name="onComplete">Callback invoked when response is ready.
        /// MUST be invoked on the Unity main thread.</param>
        /// <param name="onStreamChunk">Optional callback invoked for each streaming chunk (accumulated text).
        /// MUST be invoked on the Unity main thread. Only called if streaming is supported and enabled.</param>
        void Respond(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            Action<ResponderResult> onComplete,
            Action<string> onStreamChunk = null
        );

        /// <summary>
        /// Generate a response to the user's message with cancellation support.
        /// </summary>
        /// <param name="userMessage">The user's input text.</param>
        /// <param name="conversationHistory">Previous messages for context.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <param name="onComplete">Callback invoked when response is ready.
        /// MUST be invoked on the Unity main thread.</param>
        /// <param name="onStreamChunk">Optional callback invoked for each streaming chunk (accumulated text).
        /// MUST be invoked on the Unity main thread. Only called if streaming is supported and enabled.</param>
        void Respond(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            CancellationToken cancellationToken,
            Action<ResponderResult> onComplete,
            Action<string> onStreamChunk = null
        );

        /// <summary>
        /// Cancel any pending response generation.
        /// Safe to call even if no response is pending.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Whether a response is currently being generated.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Display name for this responder (for debugging/UI).
        /// </summary>
        string Name { get; }
    }
}

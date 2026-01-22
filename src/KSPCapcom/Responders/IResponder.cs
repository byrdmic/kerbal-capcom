using System;
using System.Collections.Generic;
using System.Threading;

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

        private ResponderResult(string text, bool success, string errorMessage)
        {
            Text = text;
            Success = success;
            ErrorMessage = errorMessage;
        }

        public static ResponderResult Ok(string text) =>
            new ResponderResult(text, true, null);

        public static ResponderResult Fail(string errorMessage) =>
            new ResponderResult(null, false, errorMessage);
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

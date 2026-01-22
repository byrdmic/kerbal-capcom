using System;
using System.Collections.Generic;
using System.Threading;

namespace KSPCapcom.Responders
{
    /// <summary>
    /// Simple echo responder for M1/MVP testing.
    /// Returns the user's message prefixed with "CAPCOM: ".
    /// Synchronous - callback is invoked immediately.
    /// </summary>
    public class EchoResponder : IResponder
    {
        public string Name => "EchoResponder";

        public bool IsBusy => false;

        public void Respond(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            Action<ResponderResult> onComplete,
            Action<string> onStreamChunk = null)
        {
            // Delegate to cancellation-aware overload
            Respond(userMessage, conversationHistory, CancellationToken.None, onComplete, onStreamChunk);
        }

        public void Respond(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            CancellationToken cancellationToken,
            Action<ResponderResult> onComplete,
            Action<string> onStreamChunk = null)
        {
            // Echo responder does not support streaming, so onStreamChunk is ignored
            if (onComplete == null)
            {
                CapcomCore.LogWarning("EchoResponder.Respond called with null callback");
                return;
            }

            // Check for cancellation (though sync responder is instant)
            if (cancellationToken.IsCancellationRequested)
            {
                onComplete(ResponderResult.Fail("Request cancelled"));
                return;
            }

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                onComplete(ResponderResult.Fail("Empty message"));
                return;
            }

            // Echo the user's message back
            string response = userMessage;

            // Invoke callback immediately (sync responder)
            onComplete(ResponderResult.Ok(response));
        }

        public void Cancel()
        {
            // No-op for sync responder
        }
    }
}

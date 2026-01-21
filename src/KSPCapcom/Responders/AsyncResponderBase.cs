using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KSPCapcom.Responders
{
    /// <summary>
    /// Base class for async responders that run work on background threads.
    /// Handles thread marshalling, cancellation, and IsBusy state.
    /// </summary>
    public abstract class AsyncResponderBase : IResponder
    {
        private CancellationTokenSource _cts;
        private readonly object _lock = new object();
        private volatile bool _isBusy;

        /// <summary>
        /// Display name for this responder.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Whether a response is currently being generated.
        /// </summary>
        public bool IsBusy => _isBusy;

        /// <summary>
        /// Generate a response to the user's message.
        /// Work is performed on a background thread; callback is invoked on main thread.
        /// </summary>
        public void Respond(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            Action<ResponderResult> onComplete)
        {
            Respond(userMessage, conversationHistory, CancellationToken.None, onComplete);
        }

        /// <summary>
        /// Generate a response with cancellation support.
        /// </summary>
        public void Respond(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            CancellationToken externalToken,
            Action<ResponderResult> onComplete)
        {
            if (onComplete == null)
            {
                CapcomCore.LogWarning($"{Name}.Respond called with null callback");
                return;
            }

            lock (_lock)
            {
                if (_isBusy)
                {
                    MainThreadDispatcher.Instance.Enqueue(() =>
                        onComplete(ResponderResult.Fail("Responder is busy")));
                    return;
                }

                _isBusy = true;
                _cts = new CancellationTokenSource();
            }

            // Link external token if provided
            var linkedCts = externalToken != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken)
                : null;
            var token = linkedCts?.Token ?? _cts.Token;

            // Run work on background thread
            Task.Run(async () =>
            {
                ResponderResult result;
                try
                {
                    result = await DoRespondAsync(userMessage, conversationHistory, token);
                }
                catch (OperationCanceledException)
                {
                    result = ResponderResult.Fail("Request cancelled");
                }
                catch (Exception ex)
                {
                    CapcomCore.LogError($"{Name} error: {ex.Message}");
                    result = ResponderResult.Fail($"Error: {ex.Message}");
                }
                finally
                {
                    lock (_lock)
                    {
                        _isBusy = false;
                        linkedCts?.Dispose();
                        _cts?.Dispose();
                        _cts = null;
                    }
                }

                // Marshal result back to main thread
                MainThreadDispatcher.Instance.Enqueue(() => onComplete(result));
            }, token);
        }

        /// <summary>
        /// Override to implement actual response logic.
        /// Runs on background thread - do NOT touch Unity APIs.
        /// </summary>
        /// <param name="userMessage">The user's input text.</param>
        /// <param name="conversationHistory">Previous messages for context.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The response result.</returns>
        protected abstract Task<ResponderResult> DoRespondAsync(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            CancellationToken cancellationToken);

        /// <summary>
        /// Cancel any pending response generation.
        /// </summary>
        public void Cancel()
        {
            lock (_lock)
            {
                _cts?.Cancel();
            }
        }
    }
}

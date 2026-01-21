using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KSPCapcom.Responders
{
    /// <summary>
    /// Async echo responder with configurable delay for testing.
    /// Demonstrates the async responder pattern and verifies threading behavior.
    /// </summary>
    public class DelayedEchoResponder : AsyncResponderBase
    {
        private readonly int _delayMs;

        /// <summary>
        /// Display name for this responder.
        /// </summary>
        public override string Name => "DelayedEchoResponder";

        /// <summary>
        /// Creates a new DelayedEchoResponder with the specified delay.
        /// </summary>
        /// <param name="delayMs">Delay in milliseconds before responding. Default is 2000ms.</param>
        public DelayedEchoResponder(int delayMs = 2000)
        {
            _delayMs = delayMs;
        }

        /// <summary>
        /// Generate a response after a simulated delay.
        /// </summary>
        protected override async Task<ResponderResult> DoRespondAsync(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            CancellationToken cancellationToken)
        {
            CapcomCore.Log($"DelayedEchoResponder: Starting {_delayMs}ms delay on thread {Thread.CurrentThread.ManagedThreadId}");

            // Simulate async work with cancellation support
            await Task.Delay(_delayMs, cancellationToken);

            CapcomCore.Log($"DelayedEchoResponder: Delay complete, returning response on thread {Thread.CurrentThread.ManagedThreadId}");

            return ResponderResult.Ok($"Echo: {userMessage}");
        }
    }
}

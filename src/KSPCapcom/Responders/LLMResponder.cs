using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KSPCapcom;
using KSPCapcom.LLM;

namespace KSPCapcom.Responders
{
    /// <summary>
    /// Bridge responder that wraps an ILLMConnector to satisfy the IResponder interface.
    /// Extends AsyncResponderBase for proper async handling and thread marshalling.
    /// </summary>
    public class LLMResponder : AsyncResponderBase
    {
        private readonly ILLMConnector _connector;
        private readonly PromptBuilder _promptBuilder;
        private readonly LLMRequestOptions _baseOptions;

        public override string Name => $"LLM ({_connector.Name})";

        /// <summary>
        /// Create a new LLM responder wrapping the given connector.
        /// </summary>
        /// <param name="connector">The LLM connector to use.</param>
        /// <param name="promptBuilder">The prompt builder for generating system prompts.</param>
        /// <param name="options">Base request options (optional). System prompt will be overwritten by promptBuilder.</param>
        public LLMResponder(ILLMConnector connector, PromptBuilder promptBuilder, LLMRequestOptions options = null)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
            _baseOptions = options ?? new LLMRequestOptions();
        }

        protected override async Task<ResponderResult> DoRespondAsync(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            CancellationToken cancellationToken,
            Action<string> onStreamChunk)
        {
            // Check if connector is configured
            if (!_connector.IsConfigured)
            {
                return ResponderResult.Fail("API key not configured - see secrets.cfg.template");
            }

            // Convert conversation history to LLMMessage list
            var messages = ConvertHistory(conversationHistory, userMessage);

            // Build request options with current system prompt from prompt builder
            // This ensures mode changes take effect immediately
            var options = _baseOptions.Clone();
            options.SystemPrompt = _promptBuilder.BuildSystemPrompt();

            // Log prompt version and mode for debugging
            CapcomCore.Log($"LLMResponder: Using prompt v{PromptBuilder.PromptVersion}, mode={_promptBuilder.GetCurrentMode()}");

            // Check if streaming should be used
            bool useStreaming = options.EnableStreaming
                && onStreamChunk != null
                && _connector is ILLMStreamingConnector streamingConnector
                && streamingConnector.SupportsStreaming;

            LLMResponse response;

            if (useStreaming)
            {
                // Use streaming with thread-safe callback marshalling
                var streamingConn = (ILLMStreamingConnector)_connector;

                // Wrap callback to marshal to main thread
                Action<string> threadSafeChunk = (chunk) =>
                {
                    MainThreadDispatcher.Instance.Enqueue(() => onStreamChunk(chunk));
                };

                response = await streamingConn.SendChatStreamingAsync(messages, options, threadSafeChunk, cancellationToken);
            }
            else
            {
                // Use non-streaming request
                response = await _connector.SendChatAsync(messages, options, cancellationToken);
            }

            // Convert response to ResponderResult
            if (response.Success)
            {
                return ResponderResult.Ok(response.Content);
            }

            // Map error to user-friendly message
            return ResponderResult.Fail(GetUserFriendlyError(response.Error));
        }

        /// <summary>
        /// Convert ChatMessage history to LLMMessage list.
        /// Skips system messages and pending messages.
        /// </summary>
        private List<LLMMessage> ConvertHistory(IReadOnlyList<ChatMessage> history, string currentUserMessage)
        {
            var messages = new List<LLMMessage>();

            // Convert history (skip system/pending messages)
            foreach (var msg in history)
            {
                // Skip pending messages
                if (msg.IsPending)
                    continue;

                // Skip system messages (status, errors, etc.)
                if (msg.Role == MessageRole.System)
                    continue;

                messages.Add(new LLMMessage(msg.Role, msg.Text));
            }

            // The current user message should already be in history, but ensure it's included
            // If the last message isn't from the user with matching content, add it
            bool lastIsCurrentUser = messages.Count > 0 &&
                                     messages[messages.Count - 1].Role == MessageRole.User &&
                                     messages[messages.Count - 1].Content == currentUserMessage;

            if (!lastIsCurrentUser)
            {
                messages.Add(LLMMessage.User(currentUserMessage));
            }

            return messages;
        }

        /// <summary>
        /// Convert LLMError to user-friendly error message.
        /// </summary>
        private string GetUserFriendlyError(LLMError error)
        {
            return ErrorMapper.GetUserFriendlyMessage(error);
        }
    }
}

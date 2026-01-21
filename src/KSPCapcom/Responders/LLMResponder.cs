using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly LLMRequestOptions _defaultOptions;

        /// <summary>
        /// Default system prompt for CAPCOM.
        /// </summary>
        private const string DEFAULT_SYSTEM_PROMPT =
            "You are CAPCOM, the spacecraft communicator for Kerbal Space Program. " +
            "You assist the player with mission planning, orbital mechanics, and spacecraft operations. " +
            "Keep responses concise and helpful. Use terminology familiar to KSP players.";

        public override string Name => $"LLM ({_connector.Name})";

        /// <summary>
        /// Create a new LLM responder wrapping the given connector.
        /// </summary>
        /// <param name="connector">The LLM connector to use.</param>
        /// <param name="options">Default request options (optional).</param>
        public LLMResponder(ILLMConnector connector, LLMRequestOptions options = null)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _defaultOptions = options ?? new LLMRequestOptions
            {
                SystemPrompt = DEFAULT_SYSTEM_PROMPT
            };

            // Ensure system prompt is set if not specified
            if (string.IsNullOrEmpty(_defaultOptions.SystemPrompt))
            {
                _defaultOptions.SystemPrompt = DEFAULT_SYSTEM_PROMPT;
            }
        }

        protected override async Task<ResponderResult> DoRespondAsync(
            string userMessage,
            IReadOnlyList<ChatMessage> conversationHistory,
            CancellationToken cancellationToken)
        {
            // Check if connector is configured
            if (!_connector.IsConfigured)
            {
                return ResponderResult.Fail("API key not configured - see secrets.cfg.template");
            }

            // Convert conversation history to LLMMessage list
            var messages = ConvertHistory(conversationHistory, userMessage);

            // Send request
            var response = await _connector.SendChatAsync(messages, _defaultOptions, cancellationToken);

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
            if (error == null)
                return "Unknown error occurred";

            switch (error.Type)
            {
                case LLMErrorType.NotConfigured:
                    return "API key not configured - see secrets.cfg.template";

                case LLMErrorType.Authentication:
                    return "Invalid API key";

                case LLMErrorType.Authorization:
                    return "API access denied - check your API key permissions";

                case LLMErrorType.RateLimit:
                    if (error.SuggestedRetryDelayMs > 0)
                    {
                        return $"Rate limited. Try again in {error.SuggestedRetryDelayMs / 1000} seconds.";
                    }
                    return "Rate limited. Try again later.";

                case LLMErrorType.ServerError:
                    return "OpenAI server error. Try again later.";

                case LLMErrorType.Network:
                    return "Cannot reach OpenAI. Check internet connection.";

                case LLMErrorType.Timeout:
                    return "Request timed out. Check your connection and endpoint, or try again.";

                case LLMErrorType.Cancelled:
                    return "Request cancelled";

                case LLMErrorType.ContextLengthExceeded:
                    return "Conversation too long. Try clearing history.";

                case LLMErrorType.ModelNotFound:
                    return "Model not available. Check model setting.";

                case LLMErrorType.ContentFiltered:
                    return "Response was filtered by content policy.";

                case LLMErrorType.InvalidRequest:
                    return error.Message ?? "Invalid request";

                default:
                    return error.Message ?? "An error occurred";
            }
        }
    }
}

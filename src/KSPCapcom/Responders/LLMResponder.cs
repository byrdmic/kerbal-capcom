using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KSPCapcom;
using KSPCapcom.KosDocs;
using KSPCapcom.LLM;
using KSPCapcom.LLM.OpenAI;
using KSPCapcom.Validation;

namespace KSPCapcom.Responders
{
    /// <summary>
    /// Bridge responder that wraps an ILLMConnector to satisfy the IResponder interface.
    /// Extends AsyncResponderBase for proper async handling and thread marshalling.
    /// </summary>
    public class LLMResponder : AsyncResponderBase
    {
        /// <summary>
        /// Maximum number of tool call iterations to prevent infinite loops.
        /// </summary>
        private const int MaxToolIterations = 40;

        /// <summary>
        /// Regex pattern to match kOS code blocks in markdown.
        /// </summary>
        private static readonly Regex CodeBlockPattern = new Regex(
            @"```(?:kos|kerboscript)?[\s\S]*?```",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ILLMConnector _connector;
        private readonly PromptBuilder _promptBuilder;
        private readonly LLMRequestOptions _baseOptions;
        private readonly Func<CapcomSettings> _getSettings;

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
            _getSettings = null; // Will use PromptBuilder's mode check for grounded mode
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

            // Build context for the user message (includes relevant kOS docs)
            var userContext = _promptBuilder.BuildUserContext(userMessage);
            var enrichedUserMessage = string.IsNullOrEmpty(userContext)
                ? userMessage
                : userContext + userMessage;

            // Convert conversation history to LLMMessage list
            var messages = ConvertHistory(conversationHistory, enrichedUserMessage, userMessage);

            // Build request options with current system prompt from prompt builder
            // This ensures mode changes take effect immediately
            var options = _baseOptions.Clone();
            options.SystemPrompt = _promptBuilder.BuildSystemPrompt();

            // Add kOS documentation search tool if service is ready
            if (KosDocService.Instance.IsReady)
            {
                options.Tools = BuildToolDefinitions();
                options.ToolChoice = "auto";
            }

            // Log prompt version and mode for debugging
            CapcomCore.Log($"LLMResponder: Using prompt v{PromptBuilder.PromptVersion}, mode={_promptBuilder.GetCurrentMode()}");

            // Create doc tracker for grounded mode validation
            var docTracker = new DocEntryTracker();

            // Tool call handling loop
            int iteration = 0;
            LLMResponse response = null;

            while (iteration < MaxToolIterations)
            {
                iteration++;

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    return ResponderResult.Fail("Request cancelled");
                }

                // Check if streaming should be used (disabled for tool calls after first iteration)
                bool useStreaming = iteration == 1
                    && options.EnableStreaming
                    && onStreamChunk != null
                    && _connector is ILLMStreamingConnector streamingConnector
                    && streamingConnector.SupportsStreaming;

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

                // Check for errors
                if (!response.Success)
                {
                    return ResponderResult.Fail(GetUserFriendlyError(response.Error));
                }

                // Check if we have tool calls to process
                if (!response.HasToolCalls)
                {
                    // Final response - validate if grounded mode is enabled
                    var validationResult = ValidateResponseIfNeeded(response.Content, docTracker);
                    return ResponderResult.Ok(response.Content, validationResult);
                }

                // Process tool calls
                CapcomCore.Log($"LLMResponder: Processing {response.ToolCalls.Count} tool call(s), iteration {iteration}");

                // Add assistant message with tool calls
                messages.Add(LLMMessage.AssistantWithToolCalls(response.Content, response.ToolCalls));

                // Execute each tool call and add results (with doc tracking)
                foreach (var toolCall in response.ToolCalls)
                {
                    if (toolCall?.Function == null)
                    {
                        continue;
                    }

                    var result = ToolCallHandler.Execute(toolCall.Function.Name, toolCall.Function.Arguments, docTracker);
                    messages.Add(LLMMessage.ToolResponse(toolCall.Id, result, toolCall.Function.Name));
                }
            }

            // Max iterations reached - return last available response
            CapcomCore.LogWarning($"LLMResponder: Max tool iterations ({MaxToolIterations}) reached");
            var finalValidation = ValidateResponseIfNeeded(response?.Content ?? "", docTracker);
            return ResponderResult.Ok(response?.Content ?? "", finalValidation);
        }

        /// <summary>
        /// Validate kOS identifiers in the response if grounded mode is enabled.
        /// </summary>
        private KosValidationResult ValidateResponseIfNeeded(string content, DocEntryTracker docTracker)
        {
            try
            {
                // Check if grounded mode is enabled
                if (!IsGroundedModeEnabled())
                {
                    return null; // Validation not needed
                }

                // Check if response contains code blocks
                if (!ContainsCodeBlock(content))
                {
                    return null; // No code to validate
                }

                // Extract code from code blocks
                var codeContent = ExtractCodeFromResponse(content);
                if (string.IsNullOrEmpty(codeContent))
                {
                    return null;
                }

                // Extract identifiers from the code
                var extractor = new KosIdentifierExtractor();
                var identifiers = extractor.Extract(codeContent);

                if (identifiers.IsEmpty)
                {
                    return null;
                }

                // Get doc entries for validation
                var docEntries = docTracker.GetAll();

                // Also add entries from semantic search in BuildUserContext (if any were retrieved)
                // The docTracker already captures tool call results

                // Create validator with retrieved docs and search index for suggestions
                var validator = new KosIdentifierValidator(docEntries, GetSearchIndex());

                // Validate
                var result = validator.Validate(identifiers);

                // Log validation summary
                if (result.HasUnverifiedIdentifiers)
                {
                    CapcomCore.LogWarning($"LLMResponder: Validation found {result.Unverified.Count} unverified identifier(s)");
                }
                else
                {
                    CapcomCore.Log($"LLMResponder: Validation passed - {result.Verified.Count} identifier(s) verified");
                }

                return result;
            }
            catch (Exception ex)
            {
                // Never break chat flow due to validation failure
                CapcomCore.LogWarning($"LLMResponder: Validation error: {ex.Message}");
                return KosValidationResult.Skipped("Validation failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Check if grounded mode is enabled in settings.
        /// </summary>
        private bool IsGroundedModeEnabled()
        {
            // Check the prompt for grounded mode indicator
            // The prompt builder adds "GROUNDED MODE ACTIVE" when enabled
            var systemPrompt = _promptBuilder.BuildSystemPrompt();
            return systemPrompt.Contains("GROUNDED MODE ACTIVE");
        }

        /// <summary>
        /// Check if the response contains code blocks.
        /// </summary>
        private bool ContainsCodeBlock(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            return CodeBlockPattern.IsMatch(content);
        }

        /// <summary>
        /// Extract code content from markdown code blocks.
        /// </summary>
        private string ExtractCodeFromResponse(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;

            var matches = CodeBlockPattern.Matches(content);
            if (matches.Count == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (Match match in matches)
            {
                var code = match.Value;
                // Remove the opening ``` and language specifier
                var startIndex = code.IndexOf('\n');
                if (startIndex < 0) continue;

                // Remove the closing ```
                var endIndex = code.LastIndexOf("```");
                if (endIndex <= startIndex) continue;

                var codeBody = code.Substring(startIndex + 1, endIndex - startIndex - 1);
                sb.AppendLine(codeBody);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get the search index for fuzzy suggestions.
        /// </summary>
        private KosDocIndex GetSearchIndex()
        {
            // Access the internal index through KosDocService
            // For now, return null to use the fallback substring matching
            // A future enhancement could expose the index from KosDocService
            return null;
        }

        /// <summary>
        /// Build the list of available tools for the LLM.
        /// </summary>
        private List<ToolDefinition> BuildToolDefinitions()
        {
            var tools = new List<ToolDefinition>();

            // Add kOS documentation search tool
            tools.Add(new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = KosDocTool.ToolName,
                    Description = "Search the kOS scripting language documentation for API references, including structures, suffixes, functions, and commands. Use this to verify correct kOS syntax before generating scripts.",
                    Parameters = new FunctionParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ParameterProperty>
                        {
                            ["query"] = new ParameterProperty
                            {
                                Type = "string",
                                Description = "Search query for kOS documentation. Can be an identifier like 'SHIP:VELOCITY' or 'ALTITUDE', or a natural language query like 'how to get orbit parameters'."
                            },
                            ["max_results"] = new ParameterProperty
                            {
                                Type = "integer",
                                Description = "Maximum number of results to return (1-10). Default is 5."
                            }
                        },
                        Required = new List<string> { "query" }
                    }
                }
            });

            return tools;
        }

        /// <summary>
        /// Convert ChatMessage history to LLMMessage list.
        /// Skips system messages and pending messages.
        /// </summary>
        /// <param name="history">Conversation history.</param>
        /// <param name="enrichedUserMessage">The current user message with context prepended.</param>
        /// <param name="originalUserMessage">The original user message for matching in history.</param>
        private List<LLMMessage> ConvertHistory(
            IReadOnlyList<ChatMessage> history,
            string enrichedUserMessage,
            string originalUserMessage)
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

                // For the current user message in history, use the enriched version
                if (msg.Role == MessageRole.User && msg.Text == originalUserMessage)
                {
                    messages.Add(new LLMMessage(MessageRole.User, enrichedUserMessage));
                }
                else
                {
                    messages.Add(new LLMMessage(msg.Role, msg.Text));
                }
            }

            // The current user message should already be in history, but ensure it's included
            // If the last message isn't from the user with matching content, add it
            bool lastIsCurrentUser = messages.Count > 0 &&
                                     messages[messages.Count - 1].Role == MessageRole.User &&
                                     (messages[messages.Count - 1].Content == enrichedUserMessage ||
                                      messages[messages.Count - 1].Content == originalUserMessage);

            if (!lastIsCurrentUser)
            {
                messages.Add(LLMMessage.User(enrichedUserMessage));
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

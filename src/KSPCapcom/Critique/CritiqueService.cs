using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KSPCapcom.Editor;
using KSPCapcom.LLM;
using KSPCapcom.Responders;

namespace KSPCapcom.Critique
{
    /// <summary>
    /// Service for validating craft and orchestrating critique LLM requests.
    /// Handles craft validation, prompt building, and async LLM communication.
    /// </summary>
    public class CritiqueService
    {
        private readonly ILLMConnector _connector;
        private readonly CritiquePromptBuilder _promptBuilder;

        /// <summary>
        /// Minimum number of parts required for a meaningful critique.
        /// </summary>
        public const int MinPartCount = 3;

        /// <summary>
        /// Request timeout for critique requests (longer than normal chat).
        /// </summary>
        public const int CritiqueTimeoutMs = 45000;

        /// <summary>
        /// Temperature for critique requests (focused but slightly creative).
        /// </summary>
        public const float CritiqueTemperature = 1.0f;

        /// <summary>
        /// Max tokens for critique responses.
        /// </summary>
        public const int CritiqueMaxTokens = 40960;

        /// <summary>
        /// Create a new CritiqueService.
        /// </summary>
        /// <param name="connector">The LLM connector to use for requests.</param>
        public CritiqueService(ILLMConnector connector)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _promptBuilder = new CritiquePromptBuilder();
        }

        /// <summary>
        /// Validate whether a craft can be critiqued.
        /// </summary>
        /// <param name="snapshot">The craft snapshot to validate.</param>
        /// <returns>Validation result indicating if critique is possible.</returns>
        public CritiqueValidation ValidateCraft(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return CritiqueValidation.Invalid("No craft loaded");
            }

            if (snapshot.TotalPartCount < MinPartCount)
            {
                return CritiqueValidation.Invalid($"Not enough parts to critique (need {MinPartCount}+)");
            }

            // Check for basic spacecraft components (command and propulsion)
            bool hasCommand = false;
            bool hasEngine = false;

            foreach (var control in snapshot.ControlParts)
            {
                if (control.Type == ControlType.CommandPod)
                {
                    hasCommand = true;
                    break;
                }
            }

            hasEngine = snapshot.Engines.Count > 0;

            if (!hasCommand && !hasEngine)
            {
                return CritiqueValidation.Invalid("Not a spacecraft yet (no command or engines)");
            }

            return CritiqueValidation.Valid();
        }

        /// <summary>
        /// Request a critique for the given craft snapshot.
        /// </summary>
        /// <param name="snapshot">The craft snapshot to critique.</param>
        /// <param name="cancellationToken">Token to cancel the request.</param>
        /// <param name="onComplete">Callback invoked when the critique is ready.</param>
        /// <param name="onStreamChunk">Optional callback for streaming chunks.</param>
        public void RequestCritique(
            EditorCraftSnapshot snapshot,
            CancellationToken cancellationToken,
            Action<ResponderResult> onComplete,
            Action<string> onStreamChunk = null)
        {
            if (snapshot == null)
            {
                onComplete?.Invoke(ResponderResult.Fail("No craft snapshot provided"));
                return;
            }

            if (onComplete == null)
            {
                CapcomCore.LogWarning("CritiqueService.RequestCritique called with null callback");
                return;
            }

            // Validate craft first
            var validation = ValidateCraft(snapshot);
            if (!validation.IsValid)
            {
                MainThreadDispatcher.Instance.Enqueue(() =>
                    onComplete(ResponderResult.Fail(validation.Reason)));
                return;
            }

            // Log the snapshot for debugging
            CapcomCore.Log($"Critique requested for '{snapshot.CraftName}' ({snapshot.TotalPartCount} parts)");
            CapcomCore.Log($"Critique snapshot: {snapshot.ToJson()}");

            // Build the critique prompt
            var systemPrompt = _promptBuilder.BuildCritiquePrompt(snapshot);
            var userMessage = _promptBuilder.BuildCritiqueUserMessage();

            CapcomCore.Log($"CritiqueService: Using prompt v{CritiquePromptBuilder.Version}");
            CapcomCore.Log($"CritiqueService: System prompt length = {systemPrompt.Length} chars");

            // Build request options
            var options = new LLMRequestOptions
            {
                SystemPrompt = systemPrompt,
                Temperature = CritiqueTemperature,
                MaxTokens = CritiqueMaxTokens,
                TimeoutMs = CritiqueTimeoutMs,
                EnableStreaming = onStreamChunk != null
            };

            // Check if connector is configured
            if (!_connector.IsConfigured)
            {
                MainThreadDispatcher.Instance.Enqueue(() =>
                    onComplete(ResponderResult.Fail("API key not configured - see secrets.cfg.template")));
                return;
            }

            // Create messages list
            var messages = new List<LLMMessage>
            {
                LLMMessage.User(userMessage)
            };

            // Run the request
            Task.Run(async () =>
            {
                ResponderResult result;
                try
                {
                    LLMResponse response;

                    // Check if streaming should be used
                    bool useStreaming = options.EnableStreaming
                        && onStreamChunk != null
                        && _connector is ILLMStreamingConnector streamingConnector
                        && streamingConnector.SupportsStreaming;

                    if (useStreaming)
                    {
                        var streamConn = (ILLMStreamingConnector)_connector;

                        // Wrap callback to marshal to main thread
                        Action<string> threadSafeChunk = (chunk) =>
                        {
                            MainThreadDispatcher.Instance.Enqueue(() => onStreamChunk(chunk));
                        };

                        response = await streamConn.SendChatStreamingAsync(messages, options, threadSafeChunk, cancellationToken);
                    }
                    else
                    {
                        response = await _connector.SendChatAsync(messages, options, cancellationToken);
                    }

                    if (response.Success)
                    {
                        result = ResponderResult.Ok(response.Content);
                    }
                    else
                    {
                        CapcomCore.Log($"CritiqueService: LLM error - Type={response.Error?.Type}, ProviderCode={response.Error?.ProviderCode}, Msg={response.Error?.Message}");
                        result = ResponderResult.Fail(ErrorMapper.GetUserFriendlyMessage(response.Error));
                    }
                }
                catch (OperationCanceledException)
                {
                    result = ResponderResult.Fail("Critique cancelled");
                }
                catch (Exception ex)
                {
                    CapcomCore.LogError($"CritiqueService error: {ex.Message}");
                    result = ResponderResult.Fail($"Error: {ex.Message}");
                }

                // Marshal result back to main thread
                MainThreadDispatcher.Instance.Enqueue(() => onComplete(result));
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Result of craft validation for critique.
    /// </summary>
    public class CritiqueValidation
    {
        /// <summary>
        /// Whether the craft is valid for critique.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Reason why validation failed (null if valid).
        /// </summary>
        public string Reason { get; }

        private CritiqueValidation(bool isValid, string reason)
        {
            IsValid = isValid;
            Reason = reason;
        }

        /// <summary>
        /// Create a valid result.
        /// </summary>
        public static CritiqueValidation Valid() => new CritiqueValidation(true, null);

        /// <summary>
        /// Create an invalid result with reason.
        /// </summary>
        public static CritiqueValidation Invalid(string reason) => new CritiqueValidation(false, reason);
    }
}

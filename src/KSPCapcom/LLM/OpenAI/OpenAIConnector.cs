using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace KSPCapcom.LLM.OpenAI
{
    /// <summary>
    /// OpenAI API connector implementing ILLMStreamingConnector.
    /// Uses UnityWebRequest for async HTTP requests (compatible with KSP's Mono runtime).
    /// </summary>
    public class OpenAIConnector : ILLMStreamingConnector
    {
        private const string OPENAI_ENDPOINT = "https://api.openai.com/v1/chat/completions";
        private const string DEFAULT_MODEL = "gpt-4o-mini";

        private readonly Func<string> _getApiKey;
        private readonly Func<string> _getModel;

        /// <summary>
        /// Human-readable name for this connector.
        /// </summary>
        public string Name => "OpenAI";

        /// <summary>
        /// Whether this connector is properly configured and ready.
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(_getApiKey?.Invoke());

        /// <summary>
        /// Whether this connector supports streaming responses.
        /// </summary>
        public bool SupportsStreaming => true;

        /// <summary>
        /// Create a new OpenAI connector.
        /// </summary>
        /// <param name="getApiKey">Function to retrieve the API key.</param>
        /// <param name="getModel">Function to retrieve the model name (optional).</param>
        public OpenAIConnector(Func<string> getApiKey, Func<string> getModel = null)
        {
            _getApiKey = getApiKey ?? throw new ArgumentNullException(nameof(getApiKey));
            _getModel = getModel ?? (() => DEFAULT_MODEL);
        }

        /// <summary>
        /// Send a chat completion request to OpenAI.
        /// </summary>
        public async Task<LLMResponse> SendChatAsync(
            IReadOnlyList<LLMMessage> messages,
            LLMRequestOptions options,
            CancellationToken cancellationToken)
        {
            // Check configuration
            var apiKey = _getApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return LLMResponse.NotConfigured("API key not configured - see secrets.cfg.template");
            }

            // Check for cancellation
            if (cancellationToken.IsCancellationRequested)
            {
                return LLMResponse.Cancelled();
            }

            var stopwatch = Stopwatch.StartNew();
            var timeoutMs = options?.TimeoutMs ?? LLMRequestOptions.DefaultTimeoutMs;

            try
            {
                // Build request
                var request = BuildRequest(messages, options, streaming: false);
                var jsonContent = request.ToJson();
                var jsonBytes = Encoding.UTF8.GetBytes(jsonContent);

                // Create UnityWebRequest
                using (var webRequest = new UnityWebRequest(OPENAI_ENDPOINT, "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                    // Send request
                    var asyncOp = webRequest.SendWebRequest();

                    // Poll until complete, cancelled, or timeout
                    while (!asyncOp.isDone)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            webRequest.Abort();
                            return LLMResponse.Cancelled();
                        }

                        if (stopwatch.ElapsedMilliseconds > timeoutMs)
                        {
                            webRequest.Abort();
                            LogRequest(stopwatch.ElapsedMilliseconds, "timeout");
                            return LLMResponse.Fail(LLMError.Timeout("Request timed out. Try again."));
                        }

                        await Task.Yield();
                    }

                    var responseBody = webRequest.downloadHandler.text;
                    var statusCode = (int)webRequest.responseCode;
                    var statusClass = GetStatusClass(statusCode);

                    LogRequest(stopwatch.ElapsedMilliseconds, statusClass);

                    // Handle connection errors (using legacy API for KSP's Unity version)
                    #pragma warning disable CS0618 // isNetworkError/isHttpError are obsolete in newer Unity
                    if (webRequest.isNetworkError)
                    {
                        var errorType = ErrorMapper.ClassifyNetworkError(webRequest.error);

                        switch (errorType)
                        {
                            case LLMErrorType.DnsResolutionFailed:
                                return LLMResponse.Fail(LLMError.DnsResolutionFailed());
                            case LLMErrorType.ConnectionRefused:
                                return LLMResponse.Fail(LLMError.ConnectionRefused());
                            default:
                                return LLMResponse.Fail(LLMError.Network());
                        }
                    }

                    // Handle success (no network error and no HTTP error)
                    if (!webRequest.isHttpError)
                    {
                        return ParseSuccessResponse(responseBody);
                    }
                    #pragma warning restore CS0618

                    // Handle HTTP errors
                    return ParseErrorResponse(statusCode, responseBody, webRequest);
                }
            }
            catch (OperationCanceledException)
            {
                return LLMResponse.Cancelled();
            }
            catch (Exception ex)
            {
                LogRequest(stopwatch.ElapsedMilliseconds, "exception");
                CapcomCore.LogError($"OpenAI request failed: {ex.Message}");
                return LLMResponse.Fail(LLMError.Unknown($"Unexpected error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Send a chat completion request with streaming response.
        /// </summary>
        public async Task<LLMResponse> SendChatStreamingAsync(
            IReadOnlyList<LLMMessage> messages,
            LLMRequestOptions options,
            Action<string> onChunk,
            CancellationToken cancellationToken)
        {
            // Check configuration
            var apiKey = _getApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return LLMResponse.NotConfigured("API key not configured - see secrets.cfg.template");
            }

            // Check for cancellation
            if (cancellationToken.IsCancellationRequested)
            {
                return LLMResponse.Cancelled();
            }

            var stopwatch = Stopwatch.StartNew();
            var timeoutMs = options?.TimeoutMs ?? LLMRequestOptions.DefaultTimeoutMs;

            try
            {
                // Build streaming request
                var request = BuildRequest(messages, options, streaming: true);
                var jsonContent = request.ToJson();
                var jsonBytes = Encoding.UTF8.GetBytes(jsonContent);

                // Debug: log outgoing request (redact actual content for privacy)
                CapcomCore.Log($"Streaming request - model={request.Model}, max_tokens={request.MaxTokens}, temp={request.Temperature}");

                // Create UnityWebRequest with streaming handler
                using (var webRequest = new UnityWebRequest(OPENAI_ENDPOINT, "POST"))
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
                    webRequest.downloadHandler = new StreamingDownloadHandler(onChunk);
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                    // Send request
                    var asyncOp = webRequest.SendWebRequest();

                    // Poll until complete, cancelled, or timeout
                    while (!asyncOp.isDone)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            webRequest.Abort();
                            return LLMResponse.Cancelled();
                        }

                        if (stopwatch.ElapsedMilliseconds > timeoutMs)
                        {
                            webRequest.Abort();
                            LogRequest(stopwatch.ElapsedMilliseconds, "timeout");
                            return LLMResponse.Fail(LLMError.Timeout("Request timed out. Try again."));
                        }

                        await Task.Yield();
                    }

                    var streamingHandler = (StreamingDownloadHandler)webRequest.downloadHandler;
                    var responseBody = streamingHandler.CompleteResponse;
                    var statusCode = (int)webRequest.responseCode;
                    var statusClass = GetStatusClass(statusCode);

                    LogRequest(stopwatch.ElapsedMilliseconds, statusClass);

                    // Handle connection errors (using legacy API for KSP's Unity version)
                    #pragma warning disable CS0618 // isNetworkError/isHttpError are obsolete in newer Unity
                    if (webRequest.isNetworkError)
                    {
                        var errorType = ErrorMapper.ClassifyNetworkError(webRequest.error);

                        switch (errorType)
                        {
                            case LLMErrorType.DnsResolutionFailed:
                                return LLMResponse.Fail(LLMError.DnsResolutionFailed());
                            case LLMErrorType.ConnectionRefused:
                                return LLMResponse.Fail(LLMError.ConnectionRefused());
                            default:
                                return LLMResponse.Fail(LLMError.Network());
                        }
                    }

                    // Handle success (no network error and no HTTP error)
                    if (!webRequest.isHttpError)
                    {
                        // Return the complete streamed response
                        return LLMResponse.Ok(responseBody, usage: null, model: request.Model);
                    }
                    #pragma warning restore CS0618

                    // Handle HTTP errors
                    return ParseErrorResponse(statusCode, responseBody, webRequest);
                }
            }
            catch (OperationCanceledException)
            {
                return LLMResponse.Cancelled();
            }
            catch (Exception ex)
            {
                LogRequest(stopwatch.ElapsedMilliseconds, "exception");
                CapcomCore.LogError($"OpenAI streaming request failed: {ex.Message}");
                return LLMResponse.Fail(LLMError.Unknown($"Unexpected error: {ex.Message}"));
            }
        }

        private ChatCompletionRequest BuildRequest(IReadOnlyList<LLMMessage> messages, LLMRequestOptions options, bool streaming = false)
        {
            var request = new ChatCompletionRequest
            {
                Model = options?.Model ?? _getModel() ?? DEFAULT_MODEL,
                Temperature = options?.Temperature ?? LLMRequestOptions.DefaultTemperature,
                MaxTokens = options?.MaxTokens ?? LLMRequestOptions.DefaultMaxTokens,
                Stream = streaming,
                Tools = options?.Tools,
                ToolChoice = options?.ToolChoice
            };

            // Add system prompt if specified
            if (!string.IsNullOrEmpty(options?.SystemPrompt))
            {
                request.Messages.Add(new ChatMessageDto("system", options.SystemPrompt));
            }

            // Convert messages
            foreach (var msg in messages)
            {
                var dto = new ChatMessageDto
                {
                    Role = ChatMessageDto.RoleToString(msg.Role),
                    Content = msg.Content
                };

                // Handle tool calls for assistant messages
                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    dto.ToolCalls = new List<ToolCall>(msg.ToolCalls);
                }

                // Handle tool response messages
                if (msg.Role == MessageRole.Tool)
                {
                    dto.ToolCallId = msg.ToolCallId;
                    dto.Name = msg.Name;
                }

                request.Messages.Add(dto);
            }

            return request;
        }

        private LLMResponse ParseSuccessResponse(string responseBody)
        {
            try
            {
                var response = JsonParser.ParseChatCompletionResponse(responseBody);
                var content = response.GetContent();
                var usage = response.Usage?.ToLLMUsage();

                // Check for tool calls
                if (response.Choices != null && response.Choices.Count > 0)
                {
                    var firstChoice = response.Choices[0];
                    if (firstChoice.ToolCalls != null && firstChoice.ToolCalls.Count > 0)
                    {
                        CapcomCore.Log($"Response contains {firstChoice.ToolCalls.Count} tool call(s)");
                        return LLMResponse.WithToolCalls(firstChoice.ToolCalls, content, usage, response.Model);
                    }
                }

                return LLMResponse.Ok(content, usage, response.Model);
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"Failed to parse OpenAI response: {ex.Message}");
                return LLMResponse.Fail(LLMError.Unknown("Failed to parse API response"));
            }
        }

        private LLMResponse ParseErrorResponse(int statusCode, string responseBody, UnityWebRequest webRequest)
        {
            // Log raw error response for diagnostics
            CapcomCore.Log($"OpenAI error response (status {statusCode}): {responseBody}");

            // Try to parse error details
            string errorMessage = null;
            string errorCode = null;

            try
            {
                var errorResponse = JsonParser.ParseErrorResponse(responseBody);
                if (errorResponse?.Error != null)
                {
                    errorMessage = errorResponse.Error.Message;
                    errorCode = errorResponse.Error.Code ?? errorResponse.Error.Type;
                }
            }
            catch
            {
                // Ignore parse errors, use generic message
            }

            // Map status codes to error types
            switch (statusCode)
            {
                case 401:
                    return LLMResponse.Fail(LLMError.Authentication(
                        errorMessage ?? "Invalid API key"));

                case 403:
                    return LLMResponse.Fail(LLMError.Authorization(
                        errorMessage ?? "Access denied"));

                case 429:
                    // Try to parse Retry-After header
                    var retryAfterMs = 5000;
                    var retryAfterHeader = webRequest.GetResponseHeader("Retry-After");
                    if (!string.IsNullOrEmpty(retryAfterHeader))
                    {
                        if (int.TryParse(retryAfterHeader, out int seconds))
                        {
                            retryAfterMs = seconds * 1000;
                        }
                    }
                    return LLMResponse.Fail(LLMError.RateLimit(
                        retryAfterMs,
                        $"Rate limited. Try again in {retryAfterMs / 1000} seconds."));

                case 400:
                    // Check for specific error types
                    if (errorCode == "context_length_exceeded")
                    {
                        return LLMResponse.Fail(LLMError.ContextLengthExceeded(
                            errorMessage ?? "Context length exceeded"));
                    }
                    if (errorCode == "model_not_found")
                    {
                        return LLMResponse.Fail(LLMError.ModelNotFound(
                            errorMessage));
                    }
                    return LLMResponse.Fail(LLMError.InvalidRequest(
                        errorMessage ?? "Invalid request"));

                case 404:
                    return LLMResponse.Fail(LLMError.ModelNotFound(
                        errorMessage ?? "Model or endpoint not found"));

                default:
                    if (statusCode >= 500)
                    {
                        return LLMResponse.Fail(LLMError.ServerError(
                            errorMessage ?? "OpenAI server error. Try again later.",
                            statusCode.ToString()));
                    }
                    return LLMResponse.Fail(LLMError.Unknown(
                        errorMessage ?? $"HTTP {statusCode}",
                        errorCode));
            }
        }

        private void LogRequest(long durationMs, string statusClass)
        {
            // Log duration and status class, never message content
            CapcomCore.Log($"OpenAI request: {durationMs}ms, {statusClass}");
        }

        private string GetStatusClass(int statusCode)
        {
            if (statusCode >= 200 && statusCode < 300) return "2xx";
            if (statusCode >= 400 && statusCode < 500) return "4xx";
            if (statusCode >= 500) return "5xx";
            return statusCode.ToString();
        }
    }
}

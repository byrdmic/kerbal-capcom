namespace KSPCapcom.LLM
{
    /// <summary>
    /// Structured error information from an LLM request.
    /// Combines typed classification with human-readable details.
    /// </summary>
    public class LLMError
    {
        /// <summary>
        /// The category of error for programmatic handling.
        /// </summary>
        public LLMErrorType Type { get; }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Provider-specific error code, if available.
        /// </summary>
        public string ProviderCode { get; }

        /// <summary>
        /// Whether this error can potentially be resolved by retrying.
        /// </summary>
        public bool IsRetryable { get; }

        /// <summary>
        /// Suggested delay in milliseconds before retrying.
        /// Only meaningful when IsRetryable is true.
        /// </summary>
        public int SuggestedRetryDelayMs { get; }

        public LLMError(
            LLMErrorType type,
            string message,
            string providerCode = null,
            bool isRetryable = false,
            int suggestedRetryDelayMs = 0)
        {
            Type = type;
            Message = message ?? "Unknown error";
            ProviderCode = providerCode;
            IsRetryable = isRetryable;
            SuggestedRetryDelayMs = suggestedRetryDelayMs;
        }

        /// <summary>
        /// No error (for successful responses).
        /// </summary>
        public static LLMError None { get; } = new LLMError(LLMErrorType.None, null);

        /// <summary>
        /// Create a cancellation error.
        /// </summary>
        public static LLMError Cancelled(string message = "Request was cancelled") =>
            new LLMError(LLMErrorType.Cancelled, message);

        /// <summary>
        /// Create a timeout error.
        /// </summary>
        public static LLMError Timeout(string message = "Request timed out") =>
            new LLMError(LLMErrorType.Timeout, message, isRetryable: true, suggestedRetryDelayMs: 1000);

        /// <summary>
        /// Create a network error.
        /// </summary>
        public static LLMError Network(string message = "Network error") =>
            new LLMError(LLMErrorType.Network, message, isRetryable: true, suggestedRetryDelayMs: 2000);

        /// <summary>
        /// Create a DNS resolution failed error.
        /// </summary>
        public static LLMError DnsResolutionFailed(string message = "Cannot resolve hostname") =>
            new LLMError(LLMErrorType.DnsResolutionFailed, message, isRetryable: true, suggestedRetryDelayMs: 5000);

        /// <summary>
        /// Create a connection refused error.
        /// </summary>
        public static LLMError ConnectionRefused(string message = "Connection refused") =>
            new LLMError(LLMErrorType.ConnectionRefused, message, isRetryable: true, suggestedRetryDelayMs: 3000);

        /// <summary>
        /// Create an authentication error.
        /// </summary>
        public static LLMError Authentication(string message = "Authentication failed") =>
            new LLMError(LLMErrorType.Authentication, message);

        /// <summary>
        /// Create an authorization error.
        /// </summary>
        public static LLMError Authorization(string message = "Access denied") =>
            new LLMError(LLMErrorType.Authorization, message);

        /// <summary>
        /// Create a rate limit error with suggested retry delay.
        /// </summary>
        public static LLMError RateLimit(int retryAfterMs = 5000, string message = "Rate limit exceeded") =>
            new LLMError(LLMErrorType.RateLimit, message, isRetryable: true, suggestedRetryDelayMs: retryAfterMs);

        /// <summary>
        /// Create an invalid request error.
        /// </summary>
        public static LLMError InvalidRequest(string message = "Invalid request") =>
            new LLMError(LLMErrorType.InvalidRequest, message);

        /// <summary>
        /// Create a model not found error.
        /// </summary>
        public static LLMError ModelNotFound(string model = null) =>
            new LLMError(LLMErrorType.ModelNotFound,
                model != null ? $"Model not found: {model}" : "Model not found");

        /// <summary>
        /// Create a context length exceeded error.
        /// </summary>
        public static LLMError ContextLengthExceeded(string message = "Context length exceeded") =>
            new LLMError(LLMErrorType.ContextLengthExceeded, message);

        /// <summary>
        /// Create a content filtered error.
        /// </summary>
        public static LLMError ContentFiltered(string message = "Content was filtered") =>
            new LLMError(LLMErrorType.ContentFiltered, message);

        /// <summary>
        /// Create a server error.
        /// </summary>
        public static LLMError ServerError(string message = "Server error", string providerCode = null) =>
            new LLMError(LLMErrorType.ServerError, message, providerCode, isRetryable: true, suggestedRetryDelayMs: 3000);

        /// <summary>
        /// Create a not configured error.
        /// </summary>
        public static LLMError NotConfigured(string message = "Connector not configured") =>
            new LLMError(LLMErrorType.NotConfigured, message);

        /// <summary>
        /// Create an unknown error.
        /// </summary>
        public static LLMError Unknown(string message, string providerCode = null) =>
            new LLMError(LLMErrorType.Unknown, message, providerCode);

        public override string ToString()
        {
            if (Type == LLMErrorType.None)
            {
                return "No error";
            }

            var result = $"[{Type}] {Message}";
            if (!string.IsNullOrEmpty(ProviderCode))
            {
                result += $" (code: {ProviderCode})";
            }
            return result;
        }
    }
}

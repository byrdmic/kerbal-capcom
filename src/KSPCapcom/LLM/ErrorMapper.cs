namespace KSPCapcom.LLM
{
    /// <summary>
    /// Static utility class for mapping LLM errors to user-friendly messages
    /// and classifying network errors into specific error types.
    /// </summary>
    public static class ErrorMapper
    {
        /// <summary>
        /// Get a user-friendly, actionable error message for the given error.
        /// </summary>
        /// <param name="error">The LLM error to map.</param>
        /// <returns>A user-friendly error message.</returns>
        public static string GetUserFriendlyMessage(LLMError error)
        {
            if (error == null)
                return "Unknown error occurred";

            switch (error.Type)
            {
                case LLMErrorType.NotConfigured:
                    return "API key not configured - see secrets.cfg.template";

                case LLMErrorType.Authentication:
                    return "Invalid API key. Check your API key in Settings.";

                case LLMErrorType.Authorization:
                    return "Access denied. Verify API key has chat permissions.";

                case LLMErrorType.RateLimit:
                    if (error.SuggestedRetryDelayMs > 0)
                    {
                        return $"Rate limited. Try again in {error.SuggestedRetryDelayMs / 1000} seconds.";
                    }
                    return "Rate limited. Try again later.";

                case LLMErrorType.ServerError:
                    return "Provider server error (5xx). Will retry automatically.";

                case LLMErrorType.Network:
                    return "Network error. Check your internet connection.";

                case LLMErrorType.DnsResolutionFailed:
                    return "Cannot resolve hostname. Check endpoint URL in Settings.";

                case LLMErrorType.ConnectionRefused:
                    return "Cannot connect to server. Is the endpoint running?";

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

        /// <summary>
        /// Classify a network error string into a specific LLMErrorType.
        /// </summary>
        /// <param name="errorString">The error string from the network layer.</param>
        /// <returns>The classified error type.</returns>
        public static LLMErrorType ClassifyNetworkError(string errorString)
        {
            if (string.IsNullOrEmpty(errorString))
                return LLMErrorType.Network;

            var errorText = errorString.ToLowerInvariant();

            if (errorText.Contains("resolve") || errorText.Contains("dns"))
                return LLMErrorType.DnsResolutionFailed;

            if (errorText.Contains("refused") || errorText.Contains("unreachable"))
                return LLMErrorType.ConnectionRefused;

            return LLMErrorType.Network;
        }
    }
}

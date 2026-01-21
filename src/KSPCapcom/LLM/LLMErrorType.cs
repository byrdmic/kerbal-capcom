namespace KSPCapcom.LLM
{
    /// <summary>
    /// Typed error categories for programmatic handling of LLM failures.
    /// Enables retry logic and user-friendly error messages.
    /// </summary>
    public enum LLMErrorType
    {
        /// <summary>No error occurred.</summary>
        None,

        /// <summary>Request was cancelled by the user or system.</summary>
        Cancelled,

        /// <summary>Network connectivity issue (DNS, connection refused, etc.).</summary>
        Network,

        /// <summary>Request timed out waiting for response.</summary>
        Timeout,

        /// <summary>Invalid or missing API key.</summary>
        Authentication,

        /// <summary>Valid credentials but insufficient permissions.</summary>
        Authorization,

        /// <summary>Rate limit exceeded, retry after delay.</summary>
        RateLimit,

        /// <summary>Malformed request (invalid parameters, bad JSON, etc.).</summary>
        InvalidRequest,

        /// <summary>Requested model does not exist or is not available.</summary>
        ModelNotFound,

        /// <summary>Input exceeds model's context window.</summary>
        ContextLengthExceeded,

        /// <summary>Content blocked by safety filters.</summary>
        ContentFiltered,

        /// <summary>Provider-side error (5xx status).</summary>
        ServerError,

        /// <summary>Connector not configured (missing endpoint, API key, etc.).</summary>
        NotConfigured,

        /// <summary>Unknown or unclassified error.</summary>
        Unknown
    }
}

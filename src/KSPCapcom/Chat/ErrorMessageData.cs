namespace KSPCapcom
{
    /// <summary>
    /// Data for rendering error messages with optional expandable details.
    /// </summary>
    public struct ErrorMessageData
    {
        /// <summary>Short, user-facing error headline.</summary>
        public string ShortMessage;

        /// <summary>Technical details (error type, timing, provider code).</summary>
        public string TechnicalDetails;

        /// <summary>Whether the error is retryable (affects color: orange vs red).</summary>
        public bool IsRetryable;

        /// <summary>Whether this was a user-initiated cancellation.</summary>
        public bool IsCancellation;

        /// <summary>Whether technical details are available.</summary>
        public bool HasDetails;
    }
}

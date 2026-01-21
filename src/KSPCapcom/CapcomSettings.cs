using System;

namespace KSPCapcom
{
    /// <summary>
    /// Operation mode for CAPCOM.
    /// Teach: Learning mode (placeholder for M2)
    /// Do: Execution mode (placeholder for M2)
    /// </summary>
    public enum OperationMode
    {
        Teach,
        Do
    }

    /// <summary>
    /// Settings container for CAPCOM configuration.
    /// In-memory only - no persistence in M1.
    /// </summary>
    public class CapcomSettings
    {
        /// <summary>
        /// Current operation mode (placeholder - does not affect behavior in M1).
        /// </summary>
        public OperationMode Mode { get; set; } = OperationMode.Teach;

        /// <summary>
        /// API endpoint URL (placeholder - stored but unused in M1).
        /// </summary>
        public string Endpoint { get; private set; } = "";

        /// <summary>
        /// Whether the current endpoint value is valid.
        /// </summary>
        public bool IsEndpointValid { get; private set; } = true;

        /// <summary>
        /// Validation error message if endpoint is invalid.
        /// </summary>
        public string EndpointValidationError { get; private set; } = "";

        /// <summary>
        /// Set the endpoint URL with validation.
        /// Empty string is valid. Otherwise must be valid http/https URL.
        /// </summary>
        /// <param name="value">The endpoint URL to set.</param>
        public void SetEndpoint(string value)
        {
            value = value ?? "";
            Endpoint = value;

            // Empty is valid
            if (string.IsNullOrWhiteSpace(value))
            {
                IsEndpointValid = true;
                EndpointValidationError = "";
                return;
            }

            // Try to parse as URI
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
            {
                IsEndpointValid = false;
                EndpointValidationError = "Invalid URL format";
                return;
            }

            // Must be http or https
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                IsEndpointValid = false;
                EndpointValidationError = "URL must use http or https";
                return;
            }

            // Valid
            IsEndpointValid = true;
            EndpointValidationError = "";
        }
    }
}

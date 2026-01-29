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
        /// Default model to use for OpenAI API requests.
        /// </summary>
        public const string DefaultModel = "gpt-5.2-2025-12-11";

        /// <summary>
        /// Current operation mode (placeholder - does not affect behavior in M1).
        /// </summary>
        public OperationMode Mode { get; set; } = OperationMode.Teach;

        /// <summary>
        /// Whether grounded mode is enabled.
        /// When enabled, the LLM will only use kOS identifiers that are verifiable
        /// from retrieved documentation and will request additional searches for unknowns.
        /// </summary>
        public bool GroundedModeEnabled { get; set; } = false;

        /// <summary>
        /// Model identifier for LLM requests.
        /// </summary>
        public string Model { get; private set; } = DefaultModel;

        /// <summary>
        /// Set the model identifier.
        /// </summary>
        /// <param name="value">The model name. If null or empty, defaults to DefaultModel.</param>
        public void SetModel(string value)
        {
            Model = string.IsNullOrWhiteSpace(value) ? DefaultModel : value;
        }

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
        /// Whether automatic retry on transient errors is enabled.
        /// Off by default.
        /// </summary>
        public bool RetryEnabled { get; set; } = false;

        private int _maxRetries = 2;

        /// <summary>
        /// Maximum number of retry attempts for transient errors.
        /// Clamped to 0-3 range. Default is 2.
        /// </summary>
        public int MaxRetries
        {
            get => _maxRetries;
            set => _maxRetries = Math.Max(0, Math.Min(value, 3));
        }

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

        /// <summary>
        /// Whether the readiness panel is visible.
        /// In-memory only - no persistence in M1.
        /// </summary>
        public bool ReadinessPanelVisible { get; set; } = false;

        /// <summary>
        /// Path to the kOS archive folder for saving scripts.
        /// </summary>
        public string KosArchivePath { get; private set; } = "";

        /// <summary>
        /// Whether the current archive path is valid (exists and is writable).
        /// </summary>
        public bool IsArchivePathValid { get; private set; } = false;

        /// <summary>
        /// Validation error message if archive path is invalid.
        /// </summary>
        public string ArchivePathValidationError { get; private set; } = "";

        /// <summary>
        /// Set the kOS archive path with validation.
        /// Empty string clears the path. Otherwise validates path exists.
        /// </summary>
        /// <param name="value">The archive folder path to set.</param>
        public void SetKosArchivePath(string value)
        {
            value = value ?? "";
            KosArchivePath = value;

            // Empty is valid (not configured)
            if (string.IsNullOrWhiteSpace(value))
            {
                IsArchivePathValid = false;
                ArchivePathValidationError = "";
                return;
            }

            // Check if directory exists
            if (!System.IO.Directory.Exists(value))
            {
                IsArchivePathValid = false;
                ArchivePathValidationError = "Archive folder not found";
                return;
            }

            // Valid
            IsArchivePathValid = true;
            ArchivePathValidationError = "";
        }
    }
}

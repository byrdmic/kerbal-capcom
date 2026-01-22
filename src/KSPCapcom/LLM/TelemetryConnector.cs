using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KSPCapcom.LLM
{
    /// <summary>
    /// Decorator that adds telemetry logging to any ILLMConnector.
    /// Logs START and END events with correlation ID, duration, and outcome.
    /// </summary>
    public class TelemetryConnector : ILLMStreamingConnector
    {
        private readonly ILLMConnector _inner;

        /// <summary>
        /// Logging action for telemetry messages.
        /// Can be overridden for testing purposes.
        /// Defaults to null; initialized by CapcomCore at runtime.
        /// </summary>
        internal static Action<string> LogAction { get; set; }

        /// <summary>
        /// Log a telemetry message using the configured LogAction.
        /// </summary>
        private static void Log(string message)
        {
            LogAction?.Invoke(message);
        }

        public string Name => _inner.Name;
        public bool IsConfigured => _inner.IsConfigured;
        public bool SupportsStreaming => (_inner is ILLMStreamingConnector s) && s.SupportsStreaming;

        public TelemetryConnector(ILLMConnector inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<LLMResponse> SendChatAsync(
            IReadOnlyList<LLMMessage> messages,
            LLMRequestOptions options,
            CancellationToken cancellationToken)
        {
            var rid = GenerateRequestId();
            var model = options?.Model ?? "default";
            var stopwatch = Stopwatch.StartNew();

            Log($"TELEM|START|rid={rid}|backend={Name}|model={model}");

            var response = await _inner.SendChatAsync(messages, options, cancellationToken);

            stopwatch.Stop();
            var (outcome, code) = ClassifyOutcome(response);

            Log($"TELEM|END|rid={rid}|ms={stopwatch.ElapsedMilliseconds}|outcome={outcome}|code={code}");

            return response;
        }

        public async Task<LLMResponse> SendChatStreamingAsync(
            IReadOnlyList<LLMMessage> messages,
            LLMRequestOptions options,
            Action<string> onChunk,
            CancellationToken cancellationToken)
        {
            if (!(_inner is ILLMStreamingConnector streamingConnector))
            {
                throw new NotSupportedException("Inner connector does not support streaming");
            }

            var rid = GenerateRequestId();
            var model = options?.Model ?? "default";
            var stopwatch = Stopwatch.StartNew();
            var chunkCount = 0;

            Log($"TELEM|START|rid={rid}|backend={Name}|model={model}|streaming=true");

            // Wrap the chunk callback to count chunks
            Action<string> wrappedOnChunk = (chunk) =>
            {
                chunkCount++;
                onChunk?.Invoke(chunk);
            };

            var response = await streamingConnector.SendChatStreamingAsync(messages, options, wrappedOnChunk, cancellationToken);

            stopwatch.Stop();
            var (outcome, code) = ClassifyOutcome(response);

            Log($"TELEM|END|rid={rid}|ms={stopwatch.ElapsedMilliseconds}|outcome={outcome}|code={code}|chunks={chunkCount}");

            return response;
        }

        /// <summary>
        /// Generate an 8-character hex request ID for correlation.
        /// </summary>
        internal static string GenerateRequestId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Classify the response outcome and error code for telemetry logging.
        /// </summary>
        internal static (string outcome, string code) ClassifyOutcome(LLMResponse response)
        {
            if (response.Success)
            {
                return ("success", "-");
            }

            if (response.IsCancelled)
            {
                return ("cancelled", "-");
            }

            if (response.Error?.Type == LLMErrorType.Timeout)
            {
                return ("timeout", "Timeout");
            }

            // Failed with an error - return the error type
            var errorCode = response.Error?.Type.ToString() ?? "Unknown";
            return ("failed", errorCode);
        }
    }
}

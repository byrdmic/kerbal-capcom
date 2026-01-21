using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KSPCapcom.LLM
{
    /// <summary>
    /// Decorator that adds retry logic to any ILLMConnector.
    /// Retries only on transient errors (IsRetryable == true) with jittered exponential backoff.
    /// </summary>
    public class RetryingConnector : ILLMConnector
    {
        private readonly ILLMConnector _inner;
        private readonly Func<bool> _getEnabled;
        private readonly Func<int> _getMaxRetries;
        private readonly Random _random = new Random();

        public string Name => _inner.Name;
        public bool IsConfigured => _inner.IsConfigured;

        public RetryingConnector(
            ILLMConnector inner,
            Func<bool> getEnabled,
            Func<int> getMaxRetries)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _getEnabled = getEnabled ?? throw new ArgumentNullException(nameof(getEnabled));
            _getMaxRetries = getMaxRetries ?? throw new ArgumentNullException(nameof(getMaxRetries));
        }

        public async Task<LLMResponse> SendChatAsync(
            IReadOnlyList<LLMMessage> messages,
            LLMRequestOptions options,
            CancellationToken cancellationToken)
        {
            // Pass through if retry is disabled
            if (!_getEnabled())
            {
                return await _inner.SendChatAsync(messages, options, cancellationToken);
            }

            var maxRetries = _getMaxRetries();
            var totalTimeoutMs = options.TimeoutMs;
            var stopwatch = Stopwatch.StartNew();
            LLMResponse lastResponse = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                // Check cancellation before each attempt
                if (cancellationToken.IsCancellationRequested)
                {
                    return LLMResponse.Cancelled();
                }

                // Calculate remaining time budget
                var elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                var remainingMs = totalTimeoutMs - elapsedMs;

                if (remainingMs <= 0)
                {
                    // Time budget exhausted
                    if (lastResponse != null)
                    {
                        CapcomCore.Log($"Retry exhausted after {attempt} attempts: time budget depleted");
                        return lastResponse;
                    }
                    return LLMResponse.Fail(LLMError.Timeout("Request timed out before first attempt"));
                }

                // Clone options with reduced timeout for this attempt
                var attemptOptions = options.Clone();
                attemptOptions.TimeoutMs = remainingMs;

                // Make the request
                lastResponse = await _inner.SendChatAsync(messages, attemptOptions, cancellationToken);

                // Success - return immediately
                if (lastResponse.Success)
                {
                    return lastResponse;
                }

                // Cancelled - never retry
                if (lastResponse.IsCancelled)
                {
                    return lastResponse;
                }

                // Non-retryable error - fail immediately
                if (!lastResponse.Error.IsRetryable)
                {
                    return lastResponse;
                }

                // Check if we have retries left
                if (attempt >= maxRetries)
                {
                    CapcomCore.Log($"Retry exhausted after {attempt + 1} attempts: {lastResponse.Error.Type}");
                    return lastResponse;
                }

                // Calculate jittered backoff delay
                var baseDelay = lastResponse.Error.SuggestedRetryDelayMs;
                if (baseDelay <= 0) baseDelay = 1000; // Fallback to 1s

                var maxDelay = baseDelay * (1 << attempt); // base * 2^attempt
                var jitteredDelay = _random.Next(0, maxDelay + 1);

                // Ensure we leave some margin in the time budget
                var updatedRemainingMs = totalTimeoutMs - (int)stopwatch.ElapsedMilliseconds;
                if (jitteredDelay >= updatedRemainingMs - 100)
                {
                    jitteredDelay = Math.Max(0, updatedRemainingMs - 100);
                }

                if (jitteredDelay <= 0)
                {
                    CapcomCore.Log($"Retry exhausted after {attempt + 1} attempts: time budget depleted");
                    return lastResponse;
                }

                CapcomCore.Log($"Retry attempt {attempt + 1}/{maxRetries}: {lastResponse.Error.Type}, waiting {jitteredDelay}ms");

                // Wait with cancellation support
                try
                {
                    await Task.Delay(jitteredDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return LLMResponse.Cancelled();
                }
            }

            // Should not reach here, but return last response as fallback
            return lastResponse ?? LLMResponse.Fail(LLMError.Unknown("Retry loop exited unexpectedly"));
        }
    }
}

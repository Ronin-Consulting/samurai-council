using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace SamurAICouncil.Core.Resilience;

/// <summary>
/// Polly resilience policies for LLM API calls.
/// </summary>
public static class LlmResiliencePolicies
{
    /// <summary>
    /// Default timeout per LLM request.
    /// </summary>
    public const int DefaultTimeoutSeconds = 60;

    /// <summary>
    /// Default number of retry attempts.
    /// </summary>
    public const int DefaultRetryCount = 3;

    /// <summary>
    /// Creates a retry policy for LLM API calls with exponential backoff.
    /// </summary>
    public static AsyncRetryPolicy CreateRetryPolicy(ILogger? logger = null)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                DefaultRetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, _) =>
                {
                    logger?.LogWarning(
                        exception,
                        "LLM API call failed (attempt {RetryCount}/{MaxRetries}). Retrying in {RetryDelay}s...",
                        retryCount, DefaultRetryCount, timeSpan.TotalSeconds);
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy for LLM API calls.
    /// Opens after 5 failures, stays open for 30 seconds.
    /// </summary>
    public static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(ILogger? logger = null)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, breakDuration) =>
                {
                    logger?.LogWarning(
                        exception,
                        "LLM circuit breaker opened for {BreakDuration}s",
                        breakDuration.TotalSeconds);
                },
                onReset: () =>
                {
                    logger?.LogInformation("LLM circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("LLM circuit breaker half-open, testing...");
                });
    }

    /// <summary>
    /// Creates a timeout policy for LLM API calls.
    /// </summary>
    public static AsyncTimeoutPolicy CreateTimeoutPolicy(int timeoutSeconds = DefaultTimeoutSeconds)
    {
        return Policy.TimeoutAsync(TimeSpan.FromSeconds(timeoutSeconds));
    }

    /// <summary>
    /// Creates a combined policy: Timeout -> Circuit Breaker -> Retry
    /// </summary>
    public static AsyncPolicy CreateCombinedPolicy(ILogger? logger = null, int timeoutSeconds = DefaultTimeoutSeconds)
    {
        var retry = CreateRetryPolicy(logger);
        var circuitBreaker = CreateCircuitBreakerPolicy(logger);
        var timeout = CreateTimeoutPolicy(timeoutSeconds);

        // Wrap in order: timeout wraps circuit breaker wraps retry
        // This means: try with retry, then check circuit breaker, then apply timeout
        return Policy.WrapAsync(timeout, circuitBreaker, retry);
    }
}

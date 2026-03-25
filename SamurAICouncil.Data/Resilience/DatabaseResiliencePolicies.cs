using Npgsql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace SamurAICouncil.Data.Resilience;

/// <summary>
/// Polly resilience policies for database operations.
/// </summary>
public static class DatabaseResiliencePolicies
{
    /// <summary>
    /// Create a retry policy for transient database errors.
    /// </summary>
    public static AsyncRetryPolicy CreateRetryPolicy(int maxRetries = 3)
    {
        return Policy
            .Handle<NpgsqlException>(ex => IsTransientError(ex))
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    // Log retry attempt (integrate with your logging framework)
                    Console.WriteLine($"Database retry {retryCount} after {timeSpan.TotalMilliseconds}ms due to: {exception.Message}");
                });
    }

    /// <summary>
    /// Create a circuit breaker policy for database operations.
    /// </summary>
    public static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(
        int exceptionsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        durationOfBreak ??= TimeSpan.FromSeconds(30);

        return Policy
            .Handle<NpgsqlException>(ex => IsTransientError(ex))
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking,
                durationOfBreak.Value,
                onBreak: (exception, duration) =>
                {
                    Console.WriteLine($"Circuit breaker opened for {duration.TotalSeconds}s due to: {exception.Message}");
                },
                onReset: () =>
                {
                    Console.WriteLine("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine("Circuit breaker half-open");
                });
    }

    /// <summary>
    /// Create a combined policy with retry and circuit breaker.
    /// </summary>
    public static AsyncPolicy CreateCombinedPolicy(
        int maxRetries = 3,
        int exceptionsAllowedBeforeBreaking = 5,
        TimeSpan? durationOfBreak = null)
    {
        var retryPolicy = CreateRetryPolicy(maxRetries);
        var circuitBreakerPolicy = CreateCircuitBreakerPolicy(exceptionsAllowedBeforeBreaking, durationOfBreak);

        // Wrap retry inside circuit breaker
        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
    }

    /// <summary>
    /// Determine if an NpgsqlException is transient and should be retried.
    /// </summary>
    private static bool IsTransientError(NpgsqlException exception)
    {
        // PostgreSQL error codes that are typically transient
        // See: https://www.postgresql.org/docs/current/errcodes-appendix.html
        var transientSqlStates = new HashSet<string>
        {
            "08000", // connection_exception
            "08003", // connection_does_not_exist
            "08006", // connection_failure
            "08001", // sqlclient_unable_to_establish_sqlconnection
            "08004", // sqlserver_rejected_establishment_of_sqlconnection
            "08007", // transaction_resolution_unknown
            "08P01", // protocol_violation
            "57P01", // admin_shutdown
            "57P02", // crash_shutdown
            "57P03", // cannot_connect_now
            "40001", // serialization_failure
            "40P01", // deadlock_detected
            "53000", // insufficient_resources
            "53100", // disk_full
            "53200", // out_of_memory
            "53300", // too_many_connections
        };

        return exception.SqlState != null && transientSqlStates.Contains(exception.SqlState);
    }
}

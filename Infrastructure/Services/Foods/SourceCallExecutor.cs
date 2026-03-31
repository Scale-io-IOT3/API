using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace Infrastructure.Services.Foods;

internal static class SourceCallExecutor
{
    public static async Task<T> ExecuteWithBudget<T>(
        Func<CancellationToken, Task<T>> sourceCall,
        SourceSettings source,
        int timeoutMs,
        string query,
        ILogger logger,
        string operation,
        Func<int, T> timeoutFactory,
        Func<T> failureFactory
    )
    {
        if (!source.Enabled)
        {
            return failureFactory();
        }

        if (SourceAvailabilityGate.IsBlocked(source.Name, out var blockedFor))
        {
            logger.LogWarning(
                "Source skipped (temporarily unavailable). source={Source}, query='{Query}', retry_in_ms={RetryInMs}",
                source.Name,
                query,
                Math.Max(1, (int)blockedFor.TotalMilliseconds)
            );
            return failureFactory();
        }

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var result = await sourceCall(cts.Token);
            SourceAvailabilityGate.MarkSuccess(source.Name);
            return result;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            SourceAvailabilityGate.MarkFailure(source.Name, source.FailureThreshold, source.Cooldown);
            logger.LogWarning(
                "Source timeout in {Operation}. source={Source}, query='{Query}', timeout_ms={TimeoutMs}",
                operation,
                source.Name,
                query,
                timeoutMs
            );
            return timeoutFactory(timeoutMs);
        }
        catch (BrokenCircuitException)
        {
            SourceAvailabilityGate.MarkFailure(source.Name, source.FailureThreshold, source.Cooldown);
            logger.LogWarning(
                "Source circuit breaker is open. source={Source}, query='{Query}'",
                source.Name,
                query
            );
            return failureFactory();
        }
        catch (Exception ex)
        {
            SourceAvailabilityGate.MarkFailure(source.Name, source.FailureThreshold, source.Cooldown);
            logger.LogWarning(
                ex,
                "Source failed in {Operation}. source={Source}, query='{Query}'",
                operation,
                source.Name,
                query
            );
            return failureFactory();
        }
    }
}

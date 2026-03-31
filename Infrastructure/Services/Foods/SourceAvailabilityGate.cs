using System.Collections.Concurrent;

namespace Infrastructure.Services.Foods;

internal static class SourceAvailabilityGate
{
    private sealed record SourceState(int ConsecutiveFailures, DateTimeOffset BlockedUntilUtc);

    private static readonly ConcurrentDictionary<string, SourceState> States = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsBlocked(string sourceName, out TimeSpan remaining)
    {
        if (States.TryGetValue(sourceName, out var state))
        {
            var now = DateTimeOffset.UtcNow;
            if (state.BlockedUntilUtc > now)
            {
                remaining = state.BlockedUntilUtc - now;
                return true;
            }
        }

        remaining = TimeSpan.Zero;
        return false;
    }

    public static void MarkSuccess(string sourceName)
    {
        States.AddOrUpdate(
            sourceName,
            _ => new SourceState(0, DateTimeOffset.MinValue),
            (_, __) => new SourceState(0, DateTimeOffset.MinValue)
        );
    }

    public static void MarkFailure(string sourceName, int failureThreshold, TimeSpan cooldown)
    {
        var threshold = Math.Max(1, failureThreshold);
        var breakFor = cooldown < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : cooldown;
        var now = DateTimeOffset.UtcNow;

        States.AddOrUpdate(
            sourceName,
            _ => threshold == 1
                ? new SourceState(0, now.Add(breakFor))
                : new SourceState(1, DateTimeOffset.MinValue),
            (_, current) =>
            {
                if (current.BlockedUntilUtc > now)
                {
                    return current;
                }

                var failures = current.ConsecutiveFailures + 1;
                return failures >= threshold
                    ? new SourceState(0, now.Add(breakFor))
                    : new SourceState(failures, DateTimeOffset.MinValue);
            }
        );
    }
}

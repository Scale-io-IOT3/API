using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services.Foods.Abstract;

/// <summary>
/// Coordinates cache access for consensus endpoints with stale-while-revalidate
/// semantics and per-key in-flight refresh de-duplication.
/// </summary>
/// <typeparam name="TEntry">Cached entry type stored in <see cref="IMemoryCache"/>.</typeparam>
/// <typeparam name="TRefreshResult">Refresh result returned to callers waiting on a refresh.</typeparam>
internal sealed class ConsensusCacheCoordinator<TEntry, TRefreshResult>(
    IMemoryCache cache,
    MemoryCacheEntryOptions cacheOptions,
    TimeSpan staleAfter,
    Func<TEntry, DateTimeOffset> refreshedAtSelector
)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<TRefreshResult>>> _inFlightRefreshes = new(StringComparer.Ordinal);
    private readonly TimeSpan _staleAfter = staleAfter < TimeSpan.FromSeconds(30)
        ? TimeSpan.FromSeconds(30)
        : staleAfter;

    /// <summary>
    /// Tries to read a cached entry and reports whether it is stale according to the configured freshness window.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="entry">Cached value when found.</param>
    /// <param name="isStale">True when the value exists but should be refreshed in the background.</param>
    /// <returns>True when a cached value exists for <paramref name="key"/>.</returns>
    public bool TryGet(string key, out TEntry? entry, out bool isStale)
    {
        if (cache.TryGetValue(key, out TEntry? cached) && cached is not null)
        {
            entry = cached;
            isStale = DateTimeOffset.UtcNow - refreshedAtSelector(cached) >= _staleAfter;
            return true;
        }

        entry = default;
        isStale = false;
        return false;
    }

    /// <summary>
    /// Tries to read a cached entry without evaluating staleness.
    /// Useful when preserving the previous successful value after a weak refresh.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="entry">Cached value when found.</param>
    /// <returns>True when a cached value exists for <paramref name="key"/>.</returns>
    public bool TryGetExisting(string key, out TEntry? entry)
    {
        if (cache.TryGetValue(key, out TEntry? cached) && cached is not null)
        {
            entry = cached;
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// Stores a cache entry using the configured cache policy.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="entry">Cache entry to store.</param>
    public void Set(string key, TEntry entry)
    {
        cache.Set(key, entry, cacheOptions);
    }

    /// <summary>
    /// Ensures only one refresh pipeline runs per cache key at a time.
    /// Concurrent callers for the same key await the same refresh task.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="refreshFactory">Factory that performs the expensive refresh work.</param>
    /// <returns>The refresh result produced by the shared task.</returns>
    public async Task<TRefreshResult> RunSharedRefreshAsync(string key, Func<Task<TRefreshResult>> refreshFactory)
    {
        var lazy = _inFlightRefreshes.GetOrAdd(
            key,
            _ => new Lazy<Task<TRefreshResult>>(refreshFactory, LazyThreadSafetyMode.ExecutionAndPublication)
        );

        Task<TRefreshResult> refreshTask;
        try
        {
            refreshTask = lazy.Value;
        }
        catch
        {
            _inFlightRefreshes.TryRemove(new KeyValuePair<string, Lazy<Task<TRefreshResult>>>(key, lazy));
            throw;
        }

        try
        {
            return await refreshTask;
        }
        finally
        {
            _inFlightRefreshes.TryRemove(new KeyValuePair<string, Lazy<Task<TRefreshResult>>>(key, lazy));
        }
    }
}

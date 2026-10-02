using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services.Foods.Metadata;

/// <summary>Caches positive and negative metadata results and shares concurrent fetches for the same key.</summary>
internal sealed class FoodMetadataCache(IMemoryCache cache)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<OpenFoodMetadata?>>> _metadataInFlight = new(StringComparer.Ordinal);

    private readonly MemoryCacheEntryOptions _metadataHitCacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };

    private readonly MemoryCacheEntryOptions _metadataMissCacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(5),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20)
    };

    /// <summary>Returns cached metadata or shares one fetch among concurrent callers for this key.</summary>
    /// <param name="cacheKey">A namespaced metadata identity key.</param>
    /// <param name="fetch">The lookup executed on a cache miss; null results are cached with a shorter lifetime.</param>
    /// <returns>The metadata, or null for a cached or newly fetched miss.</returns>
    /// <remarks>Fetch exceptions propagate. Completed or failed in-flight work is removed so later calls can retry.</remarks>
    internal async Task<OpenFoodMetadata?> GetAsync(string cacheKey, Func<Task<OpenFoodMetadata?>> fetch)
    {
        if (cache.TryGetValue(cacheKey, out OpenFoodMetadataCacheEntry? cached) && cached is not null)
        {
            return cached.Metadata;
        }

        var lazy = _metadataInFlight.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<OpenFoodMetadata?>>(
                async () =>
                {
                    var metadata = await fetch();
                    var entry = new OpenFoodMetadataCacheEntry(metadata);
                    cache.Set(cacheKey, entry, metadata is null ? _metadataMissCacheOptions : _metadataHitCacheOptions);
                    return metadata;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        Task<OpenFoodMetadata?> metadataTask;
        try
        {
            metadataTask = lazy.Value;
        }
        catch
        {
            _metadataInFlight.TryRemove(new KeyValuePair<string, Lazy<Task<OpenFoodMetadata?>>>(cacheKey, lazy));
            throw;
        }

        try
        {
            return await metadataTask;
        }
        finally
        {
            _metadataInFlight.TryRemove(new KeyValuePair<string, Lazy<Task<OpenFoodMetadata?>>>(cacheKey, lazy));
        }
    }
}

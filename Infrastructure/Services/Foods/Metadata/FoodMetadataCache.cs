using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services.Foods.Metadata;

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

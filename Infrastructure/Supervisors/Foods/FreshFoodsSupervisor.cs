using Core.DTO.Foods;
using Core.Interface.Foods.Supervisors;
using Infrastructure.Services.Foods.Search;
using Infrastructure.Services.Foods.Metadata;
using Infrastructure.Services.Foods.Abstract;
using Infrastructure.Services.Foods.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using static Infrastructure.Services.Foods.Search.SearchModels;
using static Infrastructure.Services.Foods.Search.SearchPolicy;
using static Infrastructure.Services.Foods.Search.SearchConsensusEngine;
using static Infrastructure.Services.Foods.Search.SearchResponseMapper;

namespace Infrastructure.Supervisors.Foods;

internal sealed class FreshFoodsSupervisor(
    SearchSourceProvider sources,
    FoodMetadataEnricher metadata,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<FreshFoodsSupervisor> logger
) : IFreshFoodsSupervisor
{
    private readonly ConsensusCacheCoordinator<FoodCacheEntry, RefreshResult> _cacheCoordinator = new(
        cache,
        FoodCachePolicy.CreateOptions(),
        FoodCachePolicy.ReadStaleAfter(configuration),
        static entry => entry.RefreshedAtUtc
    );

    public async Task<FoodResponse?> ResolveAsync(string normalizedQuery, double? grams = null)
    {
        var key = $"fresh_consensus_{normalizedQuery}";
        var cacheHit = false;
        var staleServed = false;

        long usdaLatency = 0;
        long openFoodLatency = 0;
        long gtinLatency = 0;
        var activeSources = 0;
        FoodCacheEntry? cacheEntry = null;

        if (_cacheCoordinator.TryGet(key, out FoodCacheEntry? cached, out var isStale) && cached is not null)
        {
            cacheHit = true;
            cacheEntry = cached;
            activeSources = cached.ActiveSources;

            if (isStale)
            {
                staleServed = true;
                _ = RefreshInBackgroundAsync(key, normalizedQuery);
            }
        }
        else
        {
            var refresh = await _cacheCoordinator.RunSharedRefreshAsync(
                key,
                () => FetchAndCacheAsync(key, normalizedQuery)
            );
            cacheEntry = refresh.Entry;
            usdaLatency = refresh.UsdaLatencyMs;
            openFoodLatency = refresh.OpenFoodLatencyMs;
            gtinLatency = refresh.GtinLatencyMs;
            activeSources = refresh.Entry.ActiveSources;
        }

        var gramsValue = grams is null or <= 0 ? 100.0 : grams.Value;
        var foods = cacheEntry!.Foods
            .Select(c => ToDto(c, gramsValue))
            .ToArray();
        await metadata.EnrichSearchAsync(foods);

        logger.LogInformation(
            "Consensus search completed. query='{Query}', normalized='{Normalized}', cache_hit={CacheHit}, stale_served={StaleServed}, usda_latency_ms={UsdaLatency}, openfood_latency_ms={OpenFoodLatency}, gtin_latency_ms={GtinLatency}, active_sources={ActiveSources}, results={ResultCount}",
            normalizedQuery,
            normalizedQuery,
            cacheHit,
            staleServed,
            usdaLatency,
            openFoodLatency,
            gtinLatency,
            activeSources,
            foods.Length
        );

        return new FoodResponse { Foods = foods };
    }

    private async Task RefreshInBackgroundAsync(string key, string normalizedQuery)
    {
        try
        {
            await _cacheCoordinator.RunSharedRefreshAsync(
                key,
                () => FetchAndCacheAsync(key, normalizedQuery)
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Consensus background refresh failed. normalized_query='{Query}'",
                normalizedQuery
            );
        }
    }

    private async Task<RefreshResult> FetchAndCacheAsync(string key, string normalizedQuery)
    {
        var usdaTask = sources.FetchUsda(normalizedQuery);
        var openFoodTask = sources.FetchOpenFood(normalizedQuery);

        await Task.WhenAll(usdaTask, openFoodTask);
        var usda = await usdaTask;
        var openFood = await openFoodTask;
        SourceCandidates gtin;
        if (ShouldQueryGtin(usda, openFood))
        {
            gtin = await sources.FetchGtinSearch(normalizedQuery);
        }
        else
        {
            gtin = new SourceCandidates([], 0);
        }

        var candidates = usda.Candidates
            .Concat(openFood.Candidates)
            .Concat(gtin.Candidates)
            .ToList();
        var activeSources = 0;
        activeSources += usda.Candidates.Count > 0 ? 1 : 0;
        activeSources += openFood.Candidates.Count > 0 ? 1 : 0;
        activeSources += gtin.Candidates.Count > 0 ? 1 : 0;

        var consensusFoods = BuildConsensus(candidates, activeSources);
        var entry = new FoodCacheEntry(consensusFoods, activeSources, DateTimeOffset.UtcNow);

        if (entry.Foods.Count == 0 &&
            _cacheCoordinator.TryGetExisting(key, out FoodCacheEntry? existing) &&
            existing is not null &&
            existing.Foods.Count > 0)
        {
            var preserved = existing with { RefreshedAtUtc = DateTimeOffset.UtcNow };
            _cacheCoordinator.Set(key, preserved);
            return new RefreshResult(
                preserved,
                usda.LatencyMs,
                openFood.LatencyMs,
                gtin.LatencyMs
            );
        }

        _cacheCoordinator.Set(key, entry);
        return new RefreshResult(
            entry,
            usda.LatencyMs,
            openFood.LatencyMs,
            gtin.LatencyMs
        );
    }

    private static bool ShouldQueryGtin(SourceCandidates usda, SourceCandidates openFood)
    {
        var primaryCount = usda.Candidates.Count + openFood.Candidates.Count;
        return primaryCount < MinPrimaryCandidatesForFastPath;
    }
}

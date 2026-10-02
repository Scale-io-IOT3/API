using Core.DTO.Foods;
using Core.Interface.Foods.Supervisors;
using Infrastructure.Services.Foods.Barcode;
using Infrastructure.Services.Foods.Metadata;
using Infrastructure.Services.Foods.Abstract;
using Infrastructure.Services.Foods.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using static Infrastructure.Services.Foods.Barcode.BarcodeModels;
using static Infrastructure.Services.Foods.Barcode.BarcodePolicy;
using static Infrastructure.Services.Foods.Barcode.BarcodeCandidateMapper;
using static Infrastructure.Services.Foods.Barcode.BarcodeConsensusEngine;
using static Infrastructure.Services.Foods.Barcode.BarcodeResponseMapper;
using static Infrastructure.Services.Foods.Shared.FoodText;

namespace Infrastructure.Supervisors.Foods;

/// <summary>Coordinates barcode identity matching, provider consensus, caching, serving scaling, and metadata enrichment.</summary>
/// <remarks>Cached nutrition is per 100 grams. Stale entries are served while a shared refresh runs in the background; response DTOs are created per request.</remarks>
internal sealed class BarcodeSupervisor(
    BarcodeSourceProvider sources,
    FoodMetadataEnricher metadata,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<BarcodeSupervisor> logger
) : IBarcodeSupervisor
{
    private readonly ConsensusCacheCoordinator<BarcodeCacheEntry, RefreshResult> _cacheCoordinator = new(
        cache,
        FoodCachePolicy.CreateOptions(),
        FoodCachePolicy.ReadStaleAfter(configuration),
        static entry => entry.RefreshedAtUtc
    );

    /// <inheritdoc />
    public async Task<FoodResponse?> ResolveAsync(string barcode, double? grams = null)
    {
        var key = $"barcode_consensus_{barcode}";
        var cacheHit = false;
        var staleServed = false;
        BarcodeCacheEntry? cacheEntry = null;

        long barcodeLatency = 0;
        long gtinBarcodeLatency = 0;
        long usdaLatency = 0;
        long openFoodLatency = 0;
        long gtinSearchLatency = 0;
        var activeSources = 0;
        string identityQuery = string.Empty;

        if (_cacheCoordinator.TryGet(key, out BarcodeCacheEntry? cached, out var isStale) && cached is not null)
        {
            cacheHit = true;
            cacheEntry = cached;
            activeSources = cached.ActiveSources;
            identityQuery = cached.IdentityQuery;

            if (isStale)
            {
                staleServed = true;
                _ = RefreshInBackgroundAsync(key, barcode);
            }
        }
        else
        {
            var refresh = await _cacheCoordinator.RunSharedRefreshAsync(
                key,
                () => FetchAndCacheAsync(key, barcode)
            );
            cacheEntry = refresh.Entry;
            barcodeLatency = refresh.BarcodeLatencyMs;
            gtinBarcodeLatency = refresh.GtinBarcodeLatencyMs;
            usdaLatency = refresh.UsdaLatencyMs;
            openFoodLatency = refresh.OpenFoodLatencyMs;
            gtinSearchLatency = refresh.GtinSearchLatencyMs;
            identityQuery = refresh.IdentityQuery;

            if (cacheEntry is null)
            {
                return new FoodResponse { Foods = [] };
            }

            activeSources = cacheEntry.ActiveSources;
        }

        var gramsValue = grams is null or <= 0 ? 100.0 : grams.Value;
        var dto = ToDto(cacheEntry!.Consensus, gramsValue);
        await metadata.EnrichBarcodeAsync(dto, barcode);

        logger.LogInformation(
            "Barcode consensus completed. input='{Input}', barcode='{Barcode}', identity_query='{IdentityQuery}', cache_hit={CacheHit}, stale_served={StaleServed}, barcode_latency_ms={BarcodeLatency}, gtin_barcode_latency_ms={GtinBarcodeLatency}, usda_latency_ms={UsdaLatency}, openfood_latency_ms={OpenFoodLatency}, gtin_search_latency_ms={GtinSearchLatency}, active_sources={ActiveSources}, confidence={Confidence}",
            barcode,
            barcode,
            identityQuery,
            cacheHit,
            staleServed,
            barcodeLatency,
            gtinBarcodeLatency,
            usdaLatency,
            openFoodLatency,
            gtinSearchLatency,
            activeSources,
            dto.Confidence
        );

        return new FoodResponse { Foods = [dto] };
    }

    private async Task RefreshInBackgroundAsync(string key, string barcode)
    {
        try
        {
            await _cacheCoordinator.RunSharedRefreshAsync(
                key,
                () => FetchAndCacheAsync(key, barcode)
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Barcode background refresh failed. barcode='{Barcode}'", barcode);
        }
    }

    private async Task<RefreshResult> FetchAndCacheAsync(string key, string barcode)
    {
        var gtinBarcodeTask = sources.FetchGtinBarcode(barcode);
        var barcodeTask = sources.FetchBarcodeAnchor(barcode);

        await Task.WhenAll(barcodeTask, gtinBarcodeTask);
        var barcodeAnchorResult = await barcodeTask;
        var gtinBarcode = await gtinBarcodeTask;

        var barcodeLatency = barcodeAnchorResult.LatencyMs;
        var gtinBarcodeLatency = gtinBarcode.LatencyMs;
        var anchorRaw = barcodeAnchorResult.Anchor ?? gtinBarcode.RawCandidates.FirstOrDefault();
        if (anchorRaw is null)
        {
            logger.LogInformation(
                "Barcode consensus found no anchor product. barcode='{Barcode}', barcode_latency_ms={BarcodeLatency}, gtin_barcode_latency_ms={GtinBarcodeLatency}",
                barcode,
                barcodeLatency,
                gtinBarcodeLatency
            );

            var preserved = TryPreserveExistingEntry(key);
            return new RefreshResult(
                preserved,
                barcodeLatency,
                gtinBarcodeLatency,
                0,
                0,
                0,
                preserved?.IdentityQuery ?? string.Empty
            );
        }

        var anchor = CreateAnchor(anchorRaw);
        var identityQuery = BuildIdentityQuery(anchor.Name, anchor.Brand);

        var usdaTask = sources.FetchUsda(identityQuery, anchor);
        var openFoodTask = sources.FetchOpenFoodSearch(identityQuery, anchor);

        await Task.WhenAll(usdaTask, openFoodTask);
        var usda = await usdaTask;
        var openFood = await openFoodTask;
        SourceCandidates gtinSearch;
        if (ShouldQueryGtinSearch(anchor, usda, openFood, gtinBarcode))
        {
            gtinSearch = await sources.FetchGtinSearch(identityQuery, anchor);
        }
        else
        {
            gtinSearch = new SourceCandidates([], 0);
        }

        var consensus = Reconcile(anchor, gtinBarcode, usda, openFood, gtinSearch);
        if (consensus.Food is null)
        {
            var preserved = TryPreserveExistingEntry(key);
            return new RefreshResult(
                preserved,
                barcodeLatency,
                gtinBarcodeLatency,
                usda.LatencyMs,
                openFood.LatencyMs,
                gtinSearch.LatencyMs,
                preserved?.IdentityQuery ?? identityQuery
            );
        }

        var entry = new BarcodeCacheEntry(
            consensus.Food,
            consensus.ActiveSources,
            DateTimeOffset.UtcNow,
            identityQuery
        );

        _cacheCoordinator.Set(key, entry);
        return new RefreshResult(
            entry,
            barcodeLatency,
            gtinBarcodeLatency,
            usda.LatencyMs,
            openFood.LatencyMs,
            gtinSearch.LatencyMs,
            identityQuery
        );
    }

    private BarcodeCacheEntry? TryPreserveExistingEntry(string key)
    {
        if (_cacheCoordinator.TryGetExisting(key, out BarcodeCacheEntry? existing) && existing is not null)
        {
            var preserved = existing with { RefreshedAtUtc = DateTimeOffset.UtcNow };
            _cacheCoordinator.Set(key, preserved);
            return preserved;
        }

        return null;
    }

    private static bool ShouldQueryGtinSearch(
        Candidate anchor,
        SourceCandidates usda,
        SourceCandidates openFood,
        RawSourceCandidates gtinBarcode
    )
    {
        if (anchor.HasNutrition)
        {
            return false;
        }

        var supportSignals = 0;
        supportSignals += usda.Candidates.Count > 0 ? 1 : 0;
        supportSignals += openFood.Candidates.Count > 0 ? 1 : 0;
        supportSignals += gtinBarcode.RawCandidates.Count > 0 ? 1 : 0;

        return supportSignals < 2;
    }
}

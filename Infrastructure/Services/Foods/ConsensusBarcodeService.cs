using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Collections.Concurrent;
using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.DTO.GtinSearch;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Core.Interface.Foods;
using Infrastructure.Services.Foods.Abstract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Foods;

/// <summary>
/// Resolves barcode nutrition using a two-stage consensus strategy:
/// identify a product anchor, then reconcile nutrition across multiple sources.
/// Includes stale-while-revalidate caching and per-key refresh de-duplication.
/// </summary>
public class ConsensusBarcodeService(
    IClient<BarcodeResponse> barcodeClient,
    IClient<FreshFoodResponse> usdaClient,
    IClient<OpenFoodSearchResponse> openFoodSearchClient,
    IClient<OpenFoodSearchALiciousResponse> openFoodSearchALiciousClient,
    IGtinSearchClient gtinSearchClient,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<ConsensusBarcodeService> logger
) : IBarcodeService
{
    private const int DefaultStaleWhileRevalidateSeconds = 1800;
    private const string BarcodeSource = "OpenFoodFactsBarcode";
    private const string UsdaSource = "USDA";
    private const string OpenFoodSearchSource = "OpenFoodFactsSearch";
    private const string GtinBarcodeSource = "GTINSearchBarcode";
    private const string GtinSearchSource = "GTINSearch";

    private const int MaxSourceCandidates = 25;
    private const int MinBarcodeLength = 8;
    private const int MaxBarcodeLength = 14;

    private const double BarcodeReliability = 0.98;
    private const double UsdaReliability = 0.95;
    private const double OpenFoodSearchReliability = 0.75;
    private const double GtinBarcodeReliability = 0.7;
    private const double GtinSearchReliability = 0.65;
    private const double MinIdentitySimilarity = 0.52;
    private const double MinMetadataSimilarity = 0.6;
    private const string BarcodeMetadataCachePrefix = "openfood_metadata_barcode_";

    private readonly SourceSettings _openFoodBarcodeSource = SourceSettingsResolver.Build(
        configuration,
        BarcodeSource,
        false,
        "OpenFoodFacts"
    );
    private readonly SourceSettings _openFoodSearchSource = SourceSettingsResolver.Build(
        configuration,
        OpenFoodSearchSource,
        false,
        "OpenFoodFacts"
    );
    private readonly SourceSettings _usdaSource = SourceSettingsResolver.Build(configuration, UsdaSource, true);
    private readonly SourceSettings _gtinBarcodeSource = SourceSettingsResolver.Build(
        configuration,
        GtinBarcodeSource,
        true,
        "GTINSearch"
    );
    private readonly SourceSettings _gtinSearchSource = SourceSettingsResolver.Build(
        configuration,
        GtinSearchSource,
        true,
        "GTINSearch"
    );
    private readonly ConsensusCacheCoordinator<BarcodeCacheEntry, RefreshResult> _cacheCoordinator = new(
        cache,
        new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(1),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
        },
        TimeSpan.FromSeconds(
            Math.Max(
                30,
                SourceSettingsResolver.ReadGlobalInt(
                    configuration,
                    "StaleWhileRevalidateSeconds",
                    DefaultStaleWhileRevalidateSeconds
                )
            )
        ),
        static entry => entry.RefreshedAtUtc
    );
    private readonly int _barcodeAnchorBudgetMs = SourceSettingsResolver.ReadTimeoutMs(
        configuration,
        BarcodeSource,
        "OpenFoodFacts",
        1200
    );
    private readonly int _gtinBarcodeBudgetMs = SourceSettingsResolver.ReadTimeoutMs(
        configuration,
        GtinBarcodeSource,
        "GTINSearch",
        1800
    );
    private readonly int _usdaBudgetMs = SourceSettingsResolver.ReadTimeoutMs(configuration, UsdaSource, null, 2200);
    private readonly int _openFoodBudgetMs = SourceSettingsResolver.ReadTimeoutMs(
        configuration,
        OpenFoodSearchSource,
        "OpenFoodFacts",
        1500
    );
    private readonly int _gtinSearchBudgetMs = SourceSettingsResolver.ReadTimeoutMs(
        configuration,
        GtinSearchSource,
        "GTINSearch",
        4500
    );
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

    private static readonly HashSet<string> NameKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "product_name",
        "title",
        "description"
    };

    private static readonly HashSet<string> BrandKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "brand",
        "brand_name",
        "manufacturer"
    };

    private static readonly HashSet<string> CaloriesKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "calories",
        "kcal",
        "energy",
        "energy_kcal",
        "energy-kcal",
        "energy-kcal_100g"
    };

    private static readonly HashSet<string> CarbohydrateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "carbohydrates",
        "carbs",
        "carbohydrate",
        "carbohydrates_100g"
    };

    private static readonly HashSet<string> FatKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "fat",
        "total_fat",
        "fat_100g"
    };

    private static readonly HashSet<string> ProteinKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "protein",
        "proteins",
        "protein_100g",
        "proteins_100g"
    };

    /// <summary>
    /// Returns a consensus barcode match.
    /// Cache hits return immediately; stale hits refresh in background.
    /// Cache misses use one shared refresh per barcode key.
    /// </summary>
    /// <param name="input">Raw barcode provided by the client.</param>
    /// <param name="grams">Requested serving size; defaults to 100g when omitted or invalid.</param>
    /// <returns>Single-item consensus response when a product can be resolved.</returns>
    public async Task<FoodResponse?> FetchAsync(string input, double? grams = null)
    {
        var barcode = NormalizeBarcode(input);
        if (!IsValidBarcode(barcode) || !HasValidCheckDigit(barcode))
        {
            logger.LogInformation(
                "Barcode consensus rejected. input='{Input}', normalized='{Normalized}'",
                input,
                barcode
            );

            return new FoodResponse { Foods = [] };
        }

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
        await ResolveOpenFoodMetadataAsync(dto, barcode);

        logger.LogInformation(
            "Barcode consensus completed. input='{Input}', barcode='{Barcode}', identity_query='{IdentityQuery}', cache_hit={CacheHit}, stale_served={StaleServed}, barcode_latency_ms={BarcodeLatency}, gtin_barcode_latency_ms={GtinBarcodeLatency}, usda_latency_ms={UsdaLatency}, openfood_latency_ms={OpenFoodLatency}, gtin_search_latency_ms={GtinSearchLatency}, active_sources={ActiveSources}, confidence={Confidence}",
            input,
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

    /// <summary>
    /// Refreshes stale barcode cache entries asynchronously without blocking callers.
    /// </summary>
    /// <param name="key">Cache key for this barcode.</param>
    /// <param name="barcode">Normalized barcode.</param>
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

    /// <summary>
    /// Executes the full barcode consensus pipeline and updates cache.
    /// Pipeline: anchor lookup -> identity query -> multi-source nutrition consensus.
    /// </summary>
    /// <param name="key">Cache key for this barcode.</param>
    /// <param name="barcode">Normalized barcode.</param>
    /// <returns>Refresh payload with cache entry and per-source timings.</returns>
    private async Task<RefreshResult> FetchAndCacheAsync(string key, string barcode)
    {
        var gtinBarcodeTask = RunWithBudgetRaw(
            token => FetchGtinBarcode(barcode, token),
            _gtinBarcodeSource,
            _gtinBarcodeBudgetMs,
            barcode
        );
        var barcodeTask = RunWithBudgetAnchor(
            token => FetchBarcodeAnchor(barcode, token),
            _openFoodBarcodeSource,
            _barcodeAnchorBudgetMs,
            barcode
        );

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

        var usdaTask = RunWithBudget(
            token => FetchUsda(identityQuery, anchor, token),
            _usdaSource,
            _usdaBudgetMs,
            identityQuery
        );
        var openFoodTask = RunWithBudget(
            token => FetchOpenFoodSearch(identityQuery, anchor, token),
            _openFoodSearchSource,
            _openFoodBudgetMs,
            identityQuery
        );

        await Task.WhenAll(usdaTask, openFoodTask);
        var usda = await usdaTask;
        var openFood = await openFoodTask;
        SourceCandidates gtinSearch;
        if (ShouldQueryGtinSearch(anchor, usda, openFood, gtinBarcode))
        {
            gtinSearch = await RunWithBudget(
                token => FetchGtinSearch(identityQuery, anchor, token),
                _gtinSearchSource,
                _gtinSearchBudgetMs,
                identityQuery
            );
        }
        else
        {
            gtinSearch = new SourceCandidates([], 0);
        }

        var candidates = new List<Candidate> { anchor };
        candidates.AddRange(gtinBarcode.RawCandidates
            .Select(raw => FinalizeRaw(raw, anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity));
        candidates.AddRange(usda.Candidates);
        candidates.AddRange(openFood.Candidates);
        candidates.AddRange(gtinSearch.Candidates);

        var aligned = AlignCandidatesToAnchor(anchor, candidates);

        var activeSourceSet = new HashSet<string>(StringComparer.Ordinal) { anchor.Source };
        if (usda.Candidates.Count > 0) activeSourceSet.Add(UsdaSource);
        if (openFood.Candidates.Count > 0) activeSourceSet.Add(OpenFoodSearchSource);
        if (gtinBarcode.RawCandidates.Count > 0) activeSourceSet.Add(GtinBarcodeSource);
        if (gtinSearch.Candidates.Count > 0) activeSourceSet.Add(GtinSearchSource);
        var activeSources = activeSourceSet.Count;

        var consensus = BuildConsensus(aligned, anchor, activeSources);
        if (consensus is null)
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
            consensus,
            activeSources,
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

    /// <summary>
    /// Preserves the previous successful cache entry when refresh results are weak/empty.
    /// </summary>
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

    /// <summary>
    /// Retrieves the anchor candidate from the primary barcode source.
    /// </summary>
    private async Task<BarcodeAnchorResult> FetchBarcodeAnchor(string barcode, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await barcodeClient.Fetch(barcode, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {BarcodeSource} returned no response payload.");
        }

        var product = response.Product;
        if (product is null || string.IsNullOrWhiteSpace(product.ResolvedName))
        {
            return new BarcodeAnchorResult(null, sw.ElapsedMilliseconds);
        }

        var macros = product.ResolvedMacros;
        var calories = ResolveCalories(macros.Calories, macros.Carbohydrates, macros.Fat, macros.Proteins);
        var raw = new RawCandidate(
            BarcodeSource,
            product.ResolvedName,
            product.ResolvedBrand,
            calories,
            macros.Carbohydrates,
            macros.Fat,
            macros.Proteins,
            BarcodeReliability,
            1.0,
            HasNutrition(macros.Calories, macros.Carbohydrates, macros.Fat, macros.Proteins),
            product.ResolvedNutritionGrade
        );

        return new BarcodeAnchorResult(raw, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Retrieves candidate products for the barcode from GTINSearch.
    /// </summary>
    private async Task<RawSourceCandidates> FetchGtinBarcode(string barcode, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var items = await gtinSearchClient.LookupBarcodeAsync(barcode, cancellationToken);
        sw.Stop();

        var raws = items
            .Select(item => ToRawFromGtin(item, GtinBarcodeSource, 1.0, GtinBarcodeReliability))
            .Where(raw => raw is not null)
            .Take(MaxSourceCandidates)
            .Cast<RawCandidate>()
            .ToList();

        return new RawSourceCandidates(raws, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Retrieves and adapts USDA search results using the resolved identity query.
    /// </summary>
    private async Task<SourceCandidates> FetchUsda(string identityQuery, Candidate anchor, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await usdaClient.Fetch(identityQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {UsdaSource} returned no response payload.");
        }

        var foods = response.FilterAndRank(identityQuery, MaxSourceCandidates).Foods;
        var candidates = foods
            .Select(FoodDto.FromFreshFood)
            .Select(dto => FinalizeRaw(ToRawFromDto(UsdaSource, dto, identityQuery, UsdaReliability), anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Retrieves and adapts OpenFoodFacts search results using the resolved identity query.
    /// </summary>
    private async Task<SourceCandidates> FetchOpenFoodSearch(
        string identityQuery,
        Candidate anchor,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var response = await openFoodSearchClient.Fetch(identityQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {OpenFoodSearchSource} returned no response payload.");
        }

        var candidates = (response.Products ?? [])
            .Select(ToRawFromOpenFoodSearch)
            .Where(raw => raw is not null)
            .Take(MaxSourceCandidates)
            .Cast<RawCandidate>()
            .Select(raw => FinalizeRaw(raw, anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Retrieves and adapts GTINSearch text results using the resolved identity query.
    /// </summary>
    private async Task<SourceCandidates> FetchGtinSearch(
        string identityQuery,
        Candidate anchor,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var items = await gtinSearchClient.SearchAsync(identityQuery, cancellationToken);
        sw.Stop();

        var candidates = items
            .Select(item => ToRawFromGtin(item, GtinSearchSource, MatchQuality(identityQuery, Normalize(item.Name ?? string.Empty)), GtinSearchReliability))
            .Where(raw => raw is not null)
            .Take(MaxSourceCandidates)
            .Cast<RawCandidate>()
            .Select(raw => FinalizeRaw(raw, anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Wraps a candidate source call with timeout, failure handling, and source-gating behavior.
    /// </summary>
    private async Task<SourceCandidates> RunWithBudget(
        Func<CancellationToken, Task<SourceCandidates>> sourceCall,
        SourceSettings source,
        int timeoutMs,
        string query
    )
    {
        return await SourceCallExecutor.ExecuteWithBudget(
            sourceCall,
            source,
            timeoutMs,
            query,
            logger,
            "barcode consensus",
            timeout => new SourceCandidates([], timeout),
            () => new SourceCandidates([], 0)
        );
    }

    /// <summary>
    /// Wraps anchor-source calls with timeout and source-gating behavior.
    /// </summary>
    private async Task<BarcodeAnchorResult> RunWithBudgetAnchor(
        Func<CancellationToken, Task<BarcodeAnchorResult>> sourceCall,
        SourceSettings source,
        int timeoutMs,
        string query
    )
    {
        return await SourceCallExecutor.ExecuteWithBudget(
            sourceCall,
            source,
            timeoutMs,
            query,
            logger,
            "barcode identity step",
            timeout => new BarcodeAnchorResult(null, timeout),
            () => new BarcodeAnchorResult(null, 0)
        );
    }

    /// <summary>
    /// Wraps raw-candidate source calls with timeout and source-gating behavior.
    /// </summary>
    private async Task<RawSourceCandidates> RunWithBudgetRaw(
        Func<CancellationToken, Task<RawSourceCandidates>> sourceCall,
        SourceSettings source,
        int timeoutMs,
        string query
    )
    {
        return await SourceCallExecutor.ExecuteWithBudget(
            sourceCall,
            source,
            timeoutMs,
            query,
            logger,
            "barcode identity step",
            timeout => new RawSourceCandidates([], timeout),
            () => new RawSourceCandidates([], 0)
        );
    }

    private static RawCandidate? ToRawFromOpenFoodSearch(OpenFoodSearchProduct product)
    {
        if (string.IsNullOrWhiteSpace(product.Name) || product.Nutriments is null)
        {
            return null;
        }

        var carbs = product.Nutriments.Carbohydrates100g ?? 0;
        var fat = product.Nutriments.Fat100g ?? 0;
        var proteins = product.Nutriments.Proteins100g ?? 0;
        var calories = ResolveCalories(product.Nutriments.EnergyKcal100g ?? 0, carbs, fat, proteins);

        return new RawCandidate(
            OpenFoodSearchSource,
            product.Name,
            product.Brands ?? string.Empty,
            calories,
            carbs,
            fat,
            proteins,
            OpenFoodSearchReliability,
            0.8,
            HasNutrition(calories, carbs, fat, proteins),
            NutritionGrade.Normalize(
                product.NutriScoreGrade,
                product.NutritionGrades,
                product.NutritionGradeFr,
                product.NutritionGradesTags?.FirstOrDefault()
            )
        );
    }

    private static RawCandidate? ToRawFromGtin(
        GtinSearchItem item,
        string source,
        double queryQuality,
        double reliability
    )
    {
        var name = FirstNonEmpty(item.Name, ExtractText(item.Extra, NameKeys));
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var brand = FirstNonEmpty(item.BrandName, item.Brand, ExtractText(item.Extra, BrandKeys));
        var carbs = ExtractNumber(item.Extra, CarbohydrateKeys) ?? 0;
        var fat = ExtractNumber(item.Extra, FatKeys) ?? 0;
        var proteins = ExtractNumber(item.Extra, ProteinKeys) ?? 0;
        var calories = ResolveCalories(ExtractNumber(item.Extra, CaloriesKeys) ?? 0, carbs, fat, proteins);

        return new RawCandidate(
            source,
            name,
            brand,
            calories,
            carbs,
            fat,
            proteins,
            reliability,
            queryQuality,
            HasNutrition(calories, carbs, fat, proteins),
            null
        );
    }

    private static RawCandidate ToRawFromDto(string source, FoodDto dto, string query, double reliability)
    {
        var calories = ResolveCalories(dto.Calories, dto.MacrosDto.Carbohydrates, dto.MacrosDto.Fat, dto.MacrosDto.Proteins);
        return new RawCandidate(
            source,
            dto.Name,
            dto.Brands,
            calories,
            dto.MacrosDto.Carbohydrates,
            dto.MacrosDto.Fat,
            dto.MacrosDto.Proteins,
            reliability,
            MatchQuality(query, Normalize(dto.Name)),
            HasNutrition(dto.Calories, dto.MacrosDto.Carbohydrates, dto.MacrosDto.Fat, dto.MacrosDto.Proteins),
            dto.Grade
        );
    }

    private static Candidate CreateAnchor(RawCandidate raw)
    {
        var normalizedName = Normalize(raw.Name);
        var normalizedBrand = Normalize(raw.Brand);
        var weight = Math.Max(0.1, raw.Reliability);

        return new Candidate(
            raw.Source,
            raw.Name,
            raw.Brand,
            normalizedName,
            normalizedBrand,
            raw.Calories,
            raw.Carbohydrates,
            raw.Fat,
            raw.Proteins,
            weight,
            raw.QueryQuality,
            1.0,
            raw.HasNutrition,
            raw.Grade
        );
    }

    private static Candidate FinalizeRaw(RawCandidate raw, Candidate anchor)
    {
        var normalizedName = Normalize(raw.Name);
        var normalizedBrand = Normalize(raw.Brand);
        var identitySimilarity = IdentitySimilarity(anchor.NormalizedName, anchor.NormalizedBrand, normalizedName, normalizedBrand);
        var blendedQuality = 0.55 * raw.QueryQuality + 0.45 * identitySimilarity;
        var weight = Math.Max(0.1, raw.Reliability * blendedQuality);

        return new Candidate(
            raw.Source,
            raw.Name,
            raw.Brand,
            normalizedName,
            normalizedBrand,
            raw.Calories,
            raw.Carbohydrates,
            raw.Fat,
            raw.Proteins,
            weight,
            raw.QueryQuality,
            identitySimilarity,
            raw.HasNutrition,
            raw.Grade
        );
    }

    /// <summary>
    /// Keeps candidates that are identity-compatible with the anchor product.
    /// Falls back to the anchor when no other candidate passes the threshold.
    /// </summary>
    private static List<Candidate> AlignCandidatesToAnchor(Candidate anchor, List<Candidate> candidates)
    {
        var aligned = candidates
            .Where(candidate =>
                candidate.Source.Equals(anchor.Source, StringComparison.Ordinal) ||
                candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return aligned.Count == 0 ? [anchor] : aligned;
    }

    /// <summary>
    /// Computes a final consensus profile from aligned candidates.
    /// Confidence reflects source agreement, identity agreement, sample size, and nutrient variance.
    /// </summary>
    private static ConsensusFood? BuildConsensus(List<Candidate> candidates, Candidate anchor, int activeSources)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var nutritionCandidates = candidates.Where(candidate => candidate.HasNutrition).ToList();
        if (nutritionCandidates.Count == 0)
        {
            return null;
        }

        var calories = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Calories, candidate.Weight))]);
        var carbs = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Carbohydrates, candidate.Weight))]);
        var fat = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Fat, candidate.Weight))]);
        var proteins = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Proteins, candidate.Weight))]);
        var consensusCalories = ResolveCalories(calories.Value, carbs.Value, fat.Value, proteins.Value);

        var sourceCount = candidates.Select(candidate => candidate.Source).Distinct(StringComparer.Ordinal).Count();
        var agreeRatio = activeSources == 0 ? 0 : (double)sourceCount / activeSources;
        var variancePenalty = (calories.NormalizedMad + carbs.NormalizedMad + fat.NormalizedMad + proteins.NormalizedMad) / 4.0;
        var identityAgreement = candidates.Average(candidate => candidate.IdentitySimilarity);
        var sampleBoost = Math.Min(1.0, nutritionCandidates.Count / 4.0);
        var grade = SelectConsensusGrade(candidates);

        var confidence = Clamp(agreeRatio * identityAgreement * (1 - variancePenalty) * (0.6 + 0.4 * sampleBoost), 0.05, 0.99);

        return new ConsensusFood(
            anchor.Name,
            anchor.Brand,
            consensusCalories,
            carbs.Value,
            fat.Value,
            proteins.Value,
            confidence,
            [.. candidates
                .Select(candidate => candidate.Source)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(source => source)],
            grade
        );
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

    /// <summary>
    /// Aggregates a nutrient channel with robust outlier filtering and weighted median.
    /// </summary>
    private static AggregateResult Aggregate(List<(double Value, double Weight)> values)
    {
        if (values.Count == 0)
        {
            return new AggregateResult(0, 1);
        }

        var rawValues = values.Select(item => item.Value).ToList();
        var median = Median(rawValues);
        var deviations = rawValues.Select(value => Math.Abs(value - median)).ToList();
        var mad = Median(deviations);

        var filtered = mad <= 0
            ? values
            : [.. values.Where(item =>
            {
                var robustZ = 0.6745 * (item.Value - median) / mad;
                return Math.Abs(robustZ) <= 3.5;
            })];

        if (filtered.Count == 0)
        {
            filtered = values;
        }

        var consensus = WeightedMedian(filtered);
        var normalizedMad = consensus <= 0 ? Math.Min(1, mad) : Math.Min(1, mad / consensus);
        return new AggregateResult(consensus, normalizedMad);
    }

    private static double WeightedMedian(List<(double Value, double Weight)> values)
    {
        var ordered = values.OrderBy(item => item.Value).ToList();
        var totalWeight = ordered.Sum(item => item.Weight);
        var threshold = totalWeight / 2.0;
        var cumulative = 0.0;

        foreach (var (Value, Weight) in ordered)
        {
            cumulative += Weight;
            if (cumulative >= threshold)
            {
                return Value;
            }
        }

        return ordered[^1].Value;
    }

    private static double Median(List<double> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        var mid = ordered.Count / 2;

        return ordered.Count % 2 == 0
            ? (ordered[mid - 1] + ordered[mid]) / 2.0
            : ordered[mid];
    }

    private static FoodDto ToDto(ConsensusFood consensus, double grams)
    {
        var dto = new FoodDto
        {
            HiddenName = consensus.Name,
            Brands = consensus.Brand,
            HiddenMacrosDto = MacrosDto.From(
                consensus.Carbohydrates,
                consensus.Fat,
                consensus.Proteins,
                (int)Math.Round(consensus.Calories)
            ),
            Grade = consensus.Grade,
            Confidence = Math.Round(consensus.Confidence, 3),
            SourcesUsed = consensus.Sources
        };

        dto.Scale(grams);
        return dto;
    }

    private async Task ResolveOpenFoodMetadataAsync(FoodDto food, string barcode)
    {
        var needsGrade = NutritionGrade.Normalize(food.Grade) is null;
        var needsNutrientLevels = food.NutrientLevels is null || food.NutrientLevels.Count == 0;

        if (!needsGrade && !needsNutrientLevels)
        {
            return;
        }

        var metadata = await GetOrFetchCachedBarcodeMetadataAsync(food, barcode);
        if (metadata is null)
        {
            return;
        }

        var resolvedGrade = NutritionGrade.Normalize(metadata.Grade);
        if (needsGrade && resolvedGrade is not null)
        {
            food.Grade = resolvedGrade;
        }

        if (needsNutrientLevels && metadata.NutrientLevels is not null && metadata.NutrientLevels.Count > 0)
        {
            food.NutrientLevels = CloneNutrientLevels(metadata.NutrientLevels);
        }
    }

    private async Task<OpenFoodMetadata?> GetOrFetchCachedBarcodeMetadataAsync(FoodDto food, string barcode)
    {
        var cacheKey = $"{BarcodeMetadataCachePrefix}{barcode}";
        if (cache.TryGetValue(cacheKey, out OpenFoodMetadataCacheEntry? cached) && cached is not null)
        {
            return cached.Metadata;
        }

        var lazy = _metadataInFlight.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<OpenFoodMetadata?>>(
                async () =>
                {
                    var metadata = await FetchBarcodeMetadataAsync(food, barcode);
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

    private async Task<OpenFoodMetadata?> FetchBarcodeMetadataAsync(FoodDto food, string barcode)
    {
        OpenFoodMetadata? barcodeMetadata = null;
        try
        {
            var barcodeResponse = await barcodeClient.Fetch(barcode);
            var product = barcodeResponse?.Product;
            if (product is not null)
            {
                barcodeMetadata = new OpenFoodMetadata(
                    NutritionGrade.Normalize(product.ResolvedNutritionGrade),
                    product.ResolvedNutrientLevels
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "OpenFoodFacts barcode metadata lookup failed. barcode='{Barcode}', food='{Food}'",
                barcode,
                food.Name
            );
        }

        var hasGrade = NutritionGrade.Normalize(barcodeMetadata?.Grade) is not null;
        var hasNutrientLevels = barcodeMetadata?.NutrientLevels is not null && barcodeMetadata.NutrientLevels.Count > 0;
        if (hasGrade && hasNutrientLevels)
        {
            return barcodeMetadata;
        }

        var fallback = await FetchBestOpenFoodMetadataAsync(food);
        if (fallback is null)
        {
            return hasGrade || hasNutrientLevels ? barcodeMetadata : null;
        }

        var grade = hasGrade ? NutritionGrade.Normalize(barcodeMetadata!.Grade) : NutritionGrade.Normalize(fallback.Grade);
        var nutrientLevels = hasNutrientLevels
            ? CloneNutrientLevels(barcodeMetadata!.NutrientLevels)
            : CloneNutrientLevels(fallback.NutrientLevels);

        if (grade is null && (nutrientLevels is null || nutrientLevels.Count == 0))
        {
            return null;
        }

        return new OpenFoodMetadata(grade, nutrientLevels);
    }

    private async Task<OpenFoodMetadata?> FetchBestOpenFoodMetadataAsync(FoodDto food)
    {
        var queries = new[]
        {
            $"{food.Brands} {food.Name}".Trim(),
            food.Name
        }
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
        {
            try
            {
                var response = await openFoodSearchALiciousClient.Fetch(query);
                var metadata = SelectBestMetadata(food, response?.Hits ?? []);
                if (metadata is not null)
                {
                    return metadata;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "OpenFoodFacts metadata lookup failed. food='{Food}', query='{Query}'",
                    food.Name,
                    query
                );
            }
        }

        return null;
    }

    private static OpenFoodMetadata? SelectBestMetadata(FoodDto target, IEnumerable<OpenFoodSearchALiciousHit> products)
    {
        var normalizedName = Normalize(target.Name);
        var normalizedBrand = Normalize(target.Brands);

        var ranked = products
            .Select(product =>
            {
                var metadata = new OpenFoodMetadata(
                    NutritionGrade.Normalize(
                        product.NutriScoreGrade,
                        product.NutritionGrades,
                        product.NutritionGradeFr,
                        product.NutritionGradesTags?.FirstOrDefault()
                    ),
                    CloneNutrientLevels(product.NutrientLevels)
                );

                return new
                {
                    Metadata = metadata,
                    Name = Normalize(product.ResolvedName),
                    Brand = Normalize(product.ResolvedBrands)
                };
            })
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Name) &&
                (item.Metadata.Grade is not null ||
                 (item.Metadata.NutrientLevels is not null && item.Metadata.NutrientLevels.Count > 0)))
            .Select(item => new
            {
                item.Metadata,
                Score = 0.85 * TokenSimilarity(normalizedName, item.Name) +
                        0.15 * TokenSimilarity(normalizedBrand, item.Brand)
            })
            .OrderByDescending(item => item.Score);

        var best = ranked.FirstOrDefault(item => item.Score >= MinMetadataSimilarity);
        if (best is not null)
        {
            return best.Metadata;
        }

        return ranked.FirstOrDefault()?.Metadata;
    }

    private static Dictionary<string, string>? CloneNutrientLevels(Dictionary<string, string>? levels)
    {
        return levels is null || levels.Count == 0
            ? null
            : levels.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsValidBarcode(string barcode)
    {
        return barcode.Length is >= MinBarcodeLength and <= MaxBarcodeLength;
    }

    private static bool HasValidCheckDigit(string code)
    {
        if (code.Length < 2 || code.Any(c => !char.IsDigit(c)))
        {
            return false;
        }

        var checkDigit = code[^1] - '0';
        var sum = 0;
        var positionFromRight = 1;

        for (var i = code.Length - 2; i >= 0; i--)
        {
            var digit = code[i] - '0';
            var weight = positionFromRight % 2 == 1 ? 3 : 1;
            sum += digit * weight;
            positionFromRight++;
        }

        var computed = (10 - (sum % 10)) % 10;
        return computed == checkDigit;
    }

    private static bool IsPlausible(Candidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Name))
        {
            return false;
        }

        if (!candidate.HasNutrition)
        {
            return true;
        }

        return candidate.Calories <= 1200 &&
               candidate.Carbohydrates <= 120 &&
               candidate.Fat <= 120 &&
               candidate.Proteins <= 120;
    }

    private static bool HasNutrition(double calories, double carbs, double fat, double proteins)
    {
        return calories > 0 || carbs > 0 || fat > 0 || proteins > 0;
    }

    private static double ResolveCalories(double calories, double carbs, double fat, double proteins)
    {
        if (calories > 0)
        {
            return calories;
        }

        var computed = carbs * 4 + fat * 9 + proteins * 4;
        return computed > 0 ? computed : 0;
    }

    private static string BuildIdentityQuery(string name, string brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return Normalize(name);
        }

        return Normalize($"{brand} {name}");
    }

    /// <summary>
    /// Heuristic query-to-name quality used as a ranking/weight signal.
    /// </summary>
    private static double MatchQuality(string query, string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(normalizedName))
        {
            return 0.1;
        }

        if (normalizedName.Equals(query, StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalizedName.StartsWith(query, StringComparison.Ordinal))
        {
            return 0.9;
        }

        if (normalizedName.Contains(query, StringComparison.Ordinal))
        {
            return 0.75;
        }

        var similarity = TokenSimilarity(query, normalizedName);
        return Math.Max(0.2, 0.2 + similarity * 0.6);
    }

    /// <summary>
    /// Product-identity similarity relative to the selected anchor.
    /// </summary>
    private static double IdentitySimilarity(
        string anchorNormalizedName,
        string anchorNormalizedBrand,
        string candidateNormalizedName,
        string candidateNormalizedBrand
    )
    {
        var nameSimilarity = TokenSimilarity(anchorNormalizedName, candidateNormalizedName);
        var brandSimilarity = TokenSimilarity(anchorNormalizedBrand, candidateNormalizedBrand);

        if (string.IsNullOrWhiteSpace(anchorNormalizedBrand))
        {
            return nameSimilarity;
        }

        return 0.8 * nameSimilarity + 0.2 * brandSimilarity;
    }

    /// <summary>
    /// Jaccard similarity over tokenized strings.
    /// </summary>
    private static double TokenSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        if (left.Equals(right, StringComparison.Ordinal))
        {
            return 1;
        }

        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private static string? SelectConsensusGrade(List<Candidate> candidates)
    {
        var grades = candidates
            .Select(candidate => new
            {
                Grade = NutritionGrade.Normalize(candidate.Grade),
                candidate.Weight
            })
            .Where(item => item.Grade is not null)
            .Select(item => new
            {
                Grade = item.Grade!,
                item.Weight
            })
            .ToList();

        if (grades.Count == 0)
        {
            return null;
        }

        return grades
            .GroupBy(item => item.Grade, StringComparer.Ordinal)
            .OrderByDescending(grouped => grouped.Sum(item => item.Weight))
            .ThenByDescending(grouped => grouped.Count())
            .ThenBy(grouped => grouped.Key, StringComparer.Ordinal)
            .Select(grouped => grouped.Key)
            .FirstOrDefault();
    }

    private static string NormalizeBarcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = value.Where(char.IsDigit).ToArray();
        return new string(digits);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string? ExtractText(Dictionary<string, System.Text.Json.JsonElement> source, HashSet<string> keys)
    {
        foreach (var entry in source)
        {
            if (keys.Contains(entry.Key) && entry.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var value = entry.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            var nested = ExtractText(entry.Value, keys);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        return null;
    }

    private static string? ExtractText(System.Text.Json.JsonElement element, HashSet<string> keys)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .SelectMany(prop =>
                {
                    var direct = keys.Contains(prop.Name) && prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                        ? new[] { prop.Value.GetString() }
                        : [];
                    var nested = ExtractText(prop.Value, keys);
                    return direct.Append(nested);
                })
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray()
                .Select(item => ExtractText(item, keys))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };
    }

    private static double? ExtractNumber(Dictionary<string, System.Text.Json.JsonElement> source, HashSet<string> keys)
    {
        foreach (var entry in source)
        {
            if (keys.Contains(entry.Key))
            {
                var parsed = ParseNumber(entry.Value);
                if (parsed is not null)
                {
                    return parsed;
                }
            }

            var nested = ExtractNumber(entry.Value, keys);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static double? ExtractNumber(System.Text.Json.JsonElement element, HashSet<string> keys)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .SelectMany(prop =>
                {
                    var direct = keys.Contains(prop.Name)
                        ? new[] { ParseNumber(prop.Value) }
                        : [];
                    var nested = ExtractNumber(prop.Value, keys);
                    return direct.Append(nested);
                })
                .FirstOrDefault(value => value is not null),
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray()
                .Select(item => ExtractNumber(item, keys))
                .FirstOrDefault(value => value is not null),
            _ => null
        };
    }

    private static double? ParseNumber(System.Text.Json.JsonElement value)
    {
        if (value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed record RawCandidate(
        string Source,
        string Name,
        string Brand,
        double Calories,
        double Carbohydrates,
        double Fat,
        double Proteins,
        double Reliability,
        double QueryQuality,
        bool HasNutrition,
        string? Grade
    );

    private sealed record Candidate(
        string Source,
        string Name,
        string Brand,
        string NormalizedName,
        string NormalizedBrand,
        double Calories,
        double Carbohydrates,
        double Fat,
        double Proteins,
        double Weight,
        double MatchQuality,
        double IdentitySimilarity,
        bool HasNutrition,
        string? Grade
    );

    private sealed record OpenFoodMetadata(
        string? Grade,
        Dictionary<string, string>? NutrientLevels
    );
    private sealed record OpenFoodMetadataCacheEntry(OpenFoodMetadata? Metadata);

    private sealed record AggregateResult(double Value, double NormalizedMad);
    private sealed record SourceCandidates(List<Candidate> Candidates, long LatencyMs);
    private sealed record RawSourceCandidates(List<RawCandidate> RawCandidates, long LatencyMs);
    private sealed record BarcodeAnchorResult(RawCandidate? Anchor, long LatencyMs);
    private sealed record BarcodeCacheEntry(
        ConsensusFood Consensus,
        int ActiveSources,
        DateTimeOffset RefreshedAtUtc,
        string IdentityQuery
    );
    private sealed record RefreshResult(
        BarcodeCacheEntry? Entry,
        long BarcodeLatencyMs,
        long GtinBarcodeLatencyMs,
        long UsdaLatencyMs,
        long OpenFoodLatencyMs,
        long GtinSearchLatencyMs,
        string IdentityQuery
    );

    private sealed record ConsensusFood(
        string Name,
        string Brand,
        double Calories,
        double Carbohydrates,
        double Fat,
        double Proteins,
        double Confidence,
        string[] Sources,
        string? Grade
    );
}

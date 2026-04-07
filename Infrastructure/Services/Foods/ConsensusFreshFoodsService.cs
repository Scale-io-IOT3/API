using System.Diagnostics;
using System.Globalization;
using System.Text;
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
/// Builds food search results by combining multiple upstream sources into a consensus ranking,
/// while using stale-while-revalidate caching to keep read latency low.
/// </summary>
public class ConsensusFreshFoodsService(
    IClient<FreshFoodResponse> usdaClient,
    IClient<OpenFoodSearchResponse> openFoodClient,
    IClient<OpenFoodSearchALiciousResponse> openFoodSearchALiciousClient,
    IGtinSearchClient gtinSearchClient,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<ConsensusFreshFoodsService> logger
) : IFreshFoodsService
{
    private const int DefaultStaleWhileRevalidateSeconds = 1800;
    private const string Usda = "USDA";
    private const string OpenFoodFacts = "OpenFoodFacts";
    private const string GtinSearch = "GTINSearch";
    private const int MaxSourceCandidates = 25;
    private const int MaxResults = 10;
    private const int MinQueryLength = 2;
    private const double UsdaReliability = 0.95;
    private const double OpenFoodReliability = 0.75;
    private const double GtinSearchReliability = 0.65;
    private const double MinMetadataSimilarity = 0.6;

    private readonly SourceSettings _usdaSource = SourceSettingsResolver.Build(configuration, Usda, true);
    private readonly SourceSettings _openFoodSource = SourceSettingsResolver.Build(configuration, OpenFoodFacts, false);
    private readonly SourceSettings _gtinSource = SourceSettingsResolver.Build(configuration, GtinSearch, true);
    private readonly ConsensusCacheCoordinator<FoodCacheEntry, RefreshResult> _cacheCoordinator = new(
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
    private readonly int _usdaBudgetMs = SourceSettingsResolver.ReadTimeoutMs(configuration, Usda, null, 2200);
    private readonly int _openFoodBudgetMs = SourceSettingsResolver.ReadTimeoutMs(configuration, OpenFoodFacts, null, 1500);
    private readonly int _gtinBudgetMs = SourceSettingsResolver.ReadTimeoutMs(configuration, GtinSearch, null, 4500);

    /// <summary>
    /// Returns fresh-food search results for the input query.
    /// Cache hits return immediately; stale hits trigger a background refresh.
    /// Cache misses run one shared refresh per key (de-duplicated across concurrent callers).
    /// </summary>
    /// <param name="input">Raw food query from the client.</param>
    /// <param name="grams">Requested serving size; defaults to 100g when omitted or invalid.</param>
    /// <returns>Consensus food response with ranked candidates.</returns>
    public async Task<FoodResponse?> FetchAsync(string input, double? grams = null)
    {
        var normalizedQuery = Normalize(input);
        if (normalizedQuery.Length < MinQueryLength)
        {
            logger.LogInformation(
                "Consensus search rejected. query='{Query}', normalized='{Normalized}', min_length={MinLength}",
                input,
                normalizedQuery,
                MinQueryLength
            );
            return new FoodResponse { Foods = [] };
        }

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
        await ResolveMissingOpenFoodMetadataAsync(foods);

        logger.LogInformation(
            "Consensus search completed. query='{Query}', normalized='{Normalized}', cache_hit={CacheHit}, stale_served={StaleServed}, usda_latency_ms={UsdaLatency}, openfood_latency_ms={OpenFoodLatency}, gtin_latency_ms={GtinLatency}, active_sources={ActiveSources}, results={ResultCount}",
            input,
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

    /// <summary>
    /// Schedules a non-blocking refresh for stale cache entries.
    /// Failures are logged without affecting the already-served response.
    /// </summary>
    /// <param name="key">Cache key for this normalized query.</param>
    /// <param name="normalizedQuery">Normalized query sent to sources.</param>
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

    /// <summary>
    /// Fetches all configured sources in parallel and writes the resulting consensus entry to cache.
    /// If the new consensus is empty, keeps the last non-empty cached value to avoid regressions.
    /// </summary>
    /// <param name="key">Cache key for this normalized query.</param>
    /// <param name="normalizedQuery">Normalized query sent to upstream sources.</param>
    /// <returns>Refresh payload containing the cached entry and source latencies.</returns>
    private async Task<RefreshResult> FetchAndCacheAsync(string key, string normalizedQuery)
    {
        var usdaTask = RunWithBudget(
            token => FetchUsda(normalizedQuery, token),
            _usdaSource,
            _usdaBudgetMs,
            normalizedQuery
        );
        var gtinTask = RunWithBudget(
            token => FetchGtinSearch(normalizedQuery, token),
            _gtinSource,
            _gtinBudgetMs,
            normalizedQuery
        );
        var openFoodTask = RunWithBudget(
            token => FetchOpenFood(normalizedQuery, token),
            _openFoodSource,
            _openFoodBudgetMs,
            normalizedQuery
        );

        await Task.WhenAll(usdaTask, gtinTask, openFoodTask);
        var usda = await usdaTask;
        var gtin = await gtinTask;
        var openFood = await openFoodTask;

        var candidates = usda.Candidates
            .Concat(openFood.Candidates)
            .Concat(gtin.Candidates)
            .ToList();
        var activeSources = 0;
        activeSources += usda.Candidates.Count > 0 ? 1 : 0;
        activeSources += openFood.Candidates.Count > 0 ? 1 : 0;
        activeSources += gtin.Candidates.Count > 0 ? 1 : 0;

        var consensusFoods = BuildConsensus(candidates, normalizedQuery, activeSources);
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

    /// <summary>
    /// Wraps a source call with timeout budget, failure handling, and temporary source blocking.
    /// </summary>
    /// <param name="sourceCall">Source invocation.</param>
    /// <param name="source">Source settings and circuit state metadata.</param>
    /// <param name="timeoutMs">Per-call timeout budget in milliseconds.</param>
    /// <param name="query">Query used for diagnostics.</param>
    /// <returns>Source candidates with measured latency.</returns>
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
            "consensus search",
            timeout => new SourceCandidates([], timeout),
            () => new SourceCandidates([], 0)
        );
    }

    /// <summary>
    /// Queries USDA and maps response items into normalized consensus candidates.
    /// </summary>
    private async Task<SourceCandidates> FetchUsda(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await usdaClient.Fetch(normalizedQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {Usda} returned no response payload.");
        }

        var foods = response.FilterAndRank(normalizedQuery, MaxSourceCandidates).Foods;
        var candidates = foods
            .Select(FoodDto.FromFreshFood)
            .Select(dto => FromDto(Usda, dto, normalizedQuery, UsdaReliability))
            .Where(IsValid)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Queries OpenFoodFacts search and maps response items into normalized consensus candidates.
    /// </summary>
    private async Task<SourceCandidates> FetchOpenFood(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await openFoodClient.Fetch(normalizedQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {OpenFoodFacts} returned no response payload.");
        }

        var candidates = (response.Products ?? [])
            .Select(product => FromOpenFood(product, normalizedQuery))
            .Where(candidate => candidate is not null)
            .Take(MaxSourceCandidates)
            .Cast<Candidate>()
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Queries GTINSearch text endpoint and maps response items into normalized consensus candidates.
    /// </summary>
    private async Task<SourceCandidates> FetchGtinSearch(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await gtinSearchClient.SearchAsync(normalizedQuery, cancellationToken);
        sw.Stop();

        var candidates = (response ?? [])
            .Select(item => FromGtinSearch(item, normalizedQuery))
            .Where(candidate => candidate is not null)
            .Take(MaxSourceCandidates)
            .Cast<Candidate>()
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }


    private static Candidate? FromOpenFood(OpenFoodSearchProduct product, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(product.Name) || product.Nutriments is null)
        {
            return null;
        }

        var carbs = product.Nutriments.Carbohydrates100g ?? 0;
        var fat = product.Nutriments.Fat100g ?? 0;
        var proteins = product.Nutriments.Proteins100g ?? 0;
        var calories = ResolveCalories(product.Nutriments.EnergyKcal100g ?? 0, carbs, fat, proteins);

        var dto = new FoodDto
        {
            HiddenName = product.Name,
            Brands = product.Brands ?? string.Empty,
            HiddenMacrosDto = MacrosDto.From(carbs, fat, proteins, (int)Math.Round(calories)),
            Grade = NutritionGrade.Normalize(
                product.NutriScoreGrade,
                product.NutritionGrades,
                product.NutritionGradeFr,
                product.NutritionGradesTags?.FirstOrDefault()
            ),
            NutrientLevels = CloneNutrientLevels(product.NutrientLevels),
        };

        var candidate = FromDto(OpenFoodFacts, dto, normalizedQuery, OpenFoodReliability);
        return IsValid(candidate) ? candidate : null;
    }

    private static Candidate? FromGtinSearch(GtinSearchItem item, string normalizedQuery)
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

        var dto = new FoodDto
        {
            HiddenName = name,
            Brands = brand,
            HiddenMacrosDto = MacrosDto.From(carbs, fat, proteins, (int)Math.Round(calories))
        };

        var candidate = FromDto(GtinSearch, dto, normalizedQuery, GtinSearchReliability);
        return IsValid(candidate) ? candidate : null;
    }

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

    private static Candidate FromDto(string source, FoodDto dto, string normalizedQuery, double reliability)
    {
        var normalizedName = Normalize(dto.Name);
        var normalizedBrand = Normalize(dto.Brands);
        var matchQuality = MatchQuality(normalizedQuery, normalizedName);
        var weight = Math.Max(0.1, reliability * matchQuality);
        var calories = ResolveCalories(dto.Calories, dto.MacrosDto.Carbohydrates, dto.MacrosDto.Fat, dto.MacrosDto.Proteins);

        return new Candidate(
            source,
            dto.Name,
            dto.Brands,
            normalizedName,
            normalizedBrand,
            calories,
            dto.MacrosDto.Carbohydrates,
            dto.MacrosDto.Fat,
            dto.MacrosDto.Proteins,
            weight,
            matchQuality,
            dto.Grade
        );
    }

    private static bool IsValid(Candidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Name))
        {
            return false;
        }

        var hasNutrition = candidate.Calories > 0 ||
                           candidate.Carbohydrates > 0 ||
                           candidate.Fat > 0 ||
                           candidate.Proteins > 0;

        if (!hasNutrition)
        {
            return false;
        }

        return candidate.Calories <= 1200 &&
               candidate.Carbohydrates <= 120 &&
               candidate.Fat <= 120 &&
               candidate.Proteins <= 120;
    }

    /// <summary>
    /// Clusters source candidates and converts each cluster into a scored consensus item.
    /// </summary>
    /// <param name="candidates">Raw source candidates for the query.</param>
    /// <param name="query">Normalized query (reserved for tuning and diagnostics).</param>
    /// <param name="activeSources">Number of sources that produced at least one candidate.</param>
    /// <returns>Top consensus items ordered by relevance.</returns>
    private static List<ConsensusFood> BuildConsensus(List<Candidate> candidates, string query, int activeSources)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var groups = Cluster(candidates);
        var consensus = groups
            .Select(group => BuildConsensus(group, activeSources))
            .Where(item => item is not null)
            .Cast<ConsensusFood>()
            .OrderByDescending(item => item.Relevance)
            .Take(MaxResults)
            .ToList();

        return consensus;
    }

    /// <summary>
    /// Groups likely-identical foods using name/brand token similarity.
    /// </summary>
    private static List<List<Candidate>> Cluster(List<Candidate> candidates)
    {
        var groups = new List<List<Candidate>>();
        foreach (var candidate in candidates.OrderByDescending(c => c.MatchQuality))
        {
            var bestScore = 0.0;
            List<Candidate>? bestGroup = null;

            foreach (var group in groups)
            {
                var anchor = group[0];
                var score = 0.85 * TokenSimilarity(candidate.NormalizedName, anchor.NormalizedName) +
                            0.15 * TokenSimilarity(candidate.NormalizedBrand, anchor.NormalizedBrand);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestGroup = group;
                }
            }

            if (bestGroup is not null && bestScore >= 0.62)
            {
                bestGroup.Add(candidate);
            }
            else
            {
                groups.Add([candidate]);
            }
        }

        return groups;
    }

    /// <summary>
    /// Produces one consensus item from a cluster of matching source candidates.
    /// </summary>
    private static ConsensusFood? BuildConsensus(List<Candidate> group, int activeSources)
    {
        if (group.Count == 0)
        {
            return null;
        }

        var best = group.OrderByDescending(item => item.MatchQuality).First();
        var calories = Aggregate(group.Select(item => (item.Calories, item.Weight)).ToList());
        var carbs = Aggregate(group.Select(item => (item.Carbohydrates, item.Weight)).ToList());
        var fat = Aggregate(group.Select(item => (item.Fat, item.Weight)).ToList());
        var proteins = Aggregate(group.Select(item => (item.Proteins, item.Weight)).ToList());
        var consensusCalories = ResolveCalories(calories.Value, carbs.Value, fat.Value, proteins.Value);

        var sourceCount = group.Select(item => item.Source).Distinct(StringComparer.Ordinal).Count();
        var agreeRatio = activeSources == 0 ? 0 : (double)sourceCount / activeSources;
        var variancePenalty = (calories.NormalizedMad + carbs.NormalizedMad + fat.NormalizedMad + proteins.NormalizedMad) / 4.0;
        var sampleBoost = Math.Min(1.0, group.Count / 4.0);
        var confidence = Clamp(agreeRatio * (1 - variancePenalty) * (0.6 + 0.4 * sampleBoost), 0.05, 0.99);
        var relevance = confidence * 0.6 + best.MatchQuality * 0.4;
        var grade = SelectConsensusGrade(group);

        return new ConsensusFood(
            best.Name,
            best.Brand,
            consensusCalories,
            carbs.Value,
            fat.Value,
            proteins.Value,
            confidence,
            group.Select(item => item.Source).Distinct(StringComparer.Ordinal).OrderBy(s => s).ToArray(),
            relevance,
            grade
        );
    }

    /// <summary>
    /// Aggregates nutrient values with outlier rejection and weighted median to improve robustness.
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

        foreach (var item in ordered)
        {
            cumulative += item.Weight;
            if (cumulative >= threshold)
            {
                return item.Value;
            }
        }

        return ordered[^1].Value;
    }

    private static double Median(List<double> values)
    {
        var ordered = values.OrderBy(v => v).ToList();
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

    private async Task ResolveMissingOpenFoodMetadataAsync(FoodDto[] foods)
    {
        var missing = foods
            .Where(food =>
                NutritionGrade.Normalize(food.Grade) is null ||
                food.NutrientLevels is null ||
                food.NutrientLevels.Count == 0)
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        foreach (var food in missing)
        {
            var resolved = await FetchBestOpenFoodMetadataAsync(food);
            if (resolved is null)
            {
                continue;
            }

            if (NutritionGrade.Normalize(food.Grade) is null)
            {
                food.Grade = resolved.Grade;
            }

            if (food.NutrientLevels is null || food.NutrientLevels.Count == 0)
            {
                food.NutrientLevels = CloneNutrientLevels(resolved.NutrientLevels);
            }
        }
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

    /// <summary>
    /// Heuristic query-to-name quality used to prioritize and weight candidates.
    /// </summary>
    private static double MatchQuality(string normalizedQuery, string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery) || string.IsNullOrWhiteSpace(normalizedName))
        {
            return 0.1;
        }

        if (normalizedName.Equals(normalizedQuery, StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 0.9;
        }

        if (normalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 0.75;
        }

        var similarity = TokenSimilarity(normalizedQuery, normalizedName);
        return Math.Max(0.2, 0.2 + similarity * 0.6);
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

    private static string? SelectConsensusGrade(List<Candidate> group)
    {
        var grades = group
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

    private static double ResolveCalories(double calories, double carbs, double fat, double proteins)
    {
        if (calories > 0)
        {
            return calories;
        }

        var computed = carbs * 4 + fat * 9 + proteins * 4;
        return computed > 0 ? computed : 0;
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
        string? Grade
    );

    private sealed record OpenFoodMetadata(
        string? Grade,
        Dictionary<string, string>? NutrientLevels
    );

    private sealed record AggregateResult(double Value, double NormalizedMad);
    private sealed record SourceCandidates(List<Candidate> Candidates, long LatencyMs);
    private sealed record FoodCacheEntry(List<ConsensusFood> Foods, int ActiveSources, DateTimeOffset RefreshedAtUtc);
    private sealed record RefreshResult(
        FoodCacheEntry Entry,
        long UsdaLatencyMs,
        long OpenFoodLatencyMs,
        long GtinLatencyMs
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
        double Relevance,
        string? Grade
    );
}

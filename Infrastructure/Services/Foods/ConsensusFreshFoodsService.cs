using System.Diagnostics;
using System.Globalization;
using System.Text;
using Polly.CircuitBreaker;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.DTO.GtinSearch;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Core.Interface.Foods;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Foods;

public class ConsensusFreshFoodsService(
    IClient<FreshFoodResponse> usdaClient,
    IClient<OpenFoodSearchResponse> openFoodClient,
    IGtinSearchClient gtinSearchClient,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<ConsensusFreshFoodsService> logger
) : IFreshFoodsService
{
    private const string Usda = "USDA";
    private const string OpenFoodFacts = "OpenFoodFacts";
    private const string GtinSearch = "GTINSearch";
    private const int MaxSourceCandidates = 25;
    private const int MaxResults = 10;
    private const int MinQueryLength = 2;
    private const int UsdaBudgetMs = 900;
    private const int OpenFoodBudgetMs = 250;
    private const int GtinBudgetMs = 400;
    private const double UsdaReliability = 0.95;
    private const double OpenFoodReliability = 0.75;
    private const double GtinSearchReliability = 0.65;

    private readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };

    private readonly SourceSettings _usdaSource = BuildSourceSettings(configuration, Usda, true);
    private readonly SourceSettings _openFoodSource = BuildSourceSettings(configuration, OpenFoodFacts, false);
    private readonly SourceSettings _gtinSource = BuildSourceSettings(configuration, GtinSearch, true);

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
        var cacheHit = cache.TryGetValue(key, out List<ConsensusFood>? consensusFoods) && consensusFoods is not null;

        long usdaLatency = 0;
        long openFoodLatency = 0;
        long gtinLatency = 0;
        var activeSources = 0;

        if (!cacheHit)
        {
            var usdaTask = RunWithBudget(
                token => FetchUsda(normalizedQuery, token),
                UsdaBudgetMs,
                _usdaSource,
                normalizedQuery
            );
            var gtinTask = RunWithBudget(
                token => FetchGtinSearch(normalizedQuery, token),
                GtinBudgetMs,
                _gtinSource,
                normalizedQuery
            );
            var openFoodTask = RunWithBudget(
                token => FetchOpenFood(normalizedQuery, token),
                OpenFoodBudgetMs,
                _openFoodSource,
                normalizedQuery
            );

            await Task.WhenAll(usdaTask, gtinTask, openFoodTask);
            var usda = await usdaTask;
            var gtin = await gtinTask;
            var openFood = await openFoodTask;

            usdaLatency = usda.LatencyMs;
            openFoodLatency = openFood.LatencyMs;
            gtinLatency = gtin.LatencyMs;

            var candidates = usda.Candidates
                .Concat(openFood.Candidates)
                .Concat(gtin.Candidates)
                .ToList();
            activeSources += usda.Candidates.Count > 0 ? 1 : 0;
            activeSources += openFood.Candidates.Count > 0 ? 1 : 0;
            activeSources += gtin.Candidates.Count > 0 ? 1 : 0;

            consensusFoods = BuildConsensus(candidates, normalizedQuery, activeSources);
            cache.Set(key, consensusFoods, _cacheOptions);
        }

        var gramsValue = grams is null or <= 0 ? 100.0 : grams.Value;
        var foods = consensusFoods!
            .Select(c => ToDto(c, gramsValue))
            .ToArray();

        logger.LogInformation(
            "Consensus search completed. query='{Query}', normalized='{Normalized}', cache_hit={CacheHit}, usda_latency_ms={UsdaLatency}, openfood_latency_ms={OpenFoodLatency}, gtin_latency_ms={GtinLatency}, active_sources={ActiveSources}, results={ResultCount}",
            input,
            normalizedQuery,
            cacheHit,
            usdaLatency,
            openFoodLatency,
            gtinLatency,
            activeSources,
            foods.Length
        );

        return new FoodResponse { Foods = foods };
    }

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

    private async Task<SourceCandidates> RunWithBudget(
        Func<CancellationToken, Task<SourceCandidates>> sourceCall,
        int timeoutMs,
        SourceSettings source,
        string query
    )
    {
        if (!source.Enabled)
        {
            return new SourceCandidates([], 0);
        }

        if (SourceAvailabilityGate.IsBlocked(source.Name, out var blockedFor))
        {
            logger.LogWarning(
                "Source skipped (temporarily unavailable). source={Source}, query='{Query}', retry_in_ms={RetryInMs}",
                source.Name,
                query,
                Math.Max(1, (int)blockedFor.TotalMilliseconds)
            );
            return new SourceCandidates([], 0);
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
                "Source timeout in consensus search. source={Source}, query='{Query}', timeout_ms={TimeoutMs}",
                source.Name,
                query,
                timeoutMs
            );

            return new SourceCandidates([], timeoutMs);
        }
        catch (BrokenCircuitException)
        {
            SourceAvailabilityGate.MarkFailure(source.Name, source.FailureThreshold, source.Cooldown);
            logger.LogWarning(
                "Source circuit breaker is open. source={Source}, query='{Query}'",
                source.Name,
                query
            );
            return new SourceCandidates([], 0);
        }
        catch (Exception ex)
        {
            SourceAvailabilityGate.MarkFailure(source.Name, source.FailureThreshold, source.Cooldown);
            logger.LogWarning(
                ex,
                "Source failed in consensus search. source={Source}, query='{Query}'",
                source.Name,
                query
            );
            return new SourceCandidates([], 0);
        }
    }

    private static SourceSettings BuildSourceSettings(
        IConfiguration configuration,
        string sourceName,
        bool defaultEnabled,
        string? groupName = null
    )
    {
        var groupEnabled = groupName is null ? defaultEnabled : IsSourceEnabled(configuration, groupName, defaultEnabled);
        var enabled = IsSourceEnabled(configuration, sourceName, groupEnabled);
        var failureThreshold = ReadSourceInt(configuration, sourceName, groupName, "FailureThreshold", 2);
        var cooldownSeconds = ReadSourceInt(configuration, sourceName, groupName, "CooldownSeconds", 120);

        return new SourceSettings(
            sourceName,
            enabled,
            Math.Max(1, failureThreshold),
            TimeSpan.FromSeconds(Math.Max(5, cooldownSeconds))
        );
    }

    private static bool IsSourceEnabled(IConfiguration configuration, string sourceName, bool defaultValue)
    {
        var raw = configuration[$"Sources:{sourceName}:Enabled"];
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static int ReadSourceInt(
        IConfiguration configuration,
        string sourceName,
        string? groupName,
        string setting,
        int defaultValue
    )
    {
        var sourceRaw = configuration[$"Sources:{sourceName}:{setting}"];
        if (int.TryParse(sourceRaw, out var sourceValue))
        {
            return sourceValue;
        }

        if (!string.IsNullOrWhiteSpace(groupName))
        {
            var groupRaw = configuration[$"Sources:{groupName}:{setting}"];
            if (int.TryParse(groupRaw, out var groupValue))
            {
                return groupValue;
            }
        }

        var globalRaw = configuration[$"Sources:Global:{setting}"];
        return int.TryParse(globalRaw, out var globalValue) ? globalValue : defaultValue;
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
            HiddenMacrosDto = MacrosDto.From(carbs, fat, proteins, (int)Math.Round(calories))
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
                        : Array.Empty<string?>();
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
                        : Array.Empty<double?>();
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
            matchQuality
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

        return new ConsensusFood(
            best.Name,
            best.Brand,
            consensusCalories,
            carbs.Value,
            fat.Value,
            proteins.Value,
            confidence,
            group.Select(item => item.Source).Distinct(StringComparer.Ordinal).OrderBy(s => s).ToArray(),
            relevance
        );
    }

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
            : values.Where(item =>
            {
                var robustZ = 0.6745 * (item.Value - median) / mad;
                return Math.Abs(robustZ) <= 3.5;
            }).ToList();

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
            Confidence = Math.Round(consensus.Confidence, 3),
            SourcesUsed = consensus.Sources
        };

        dto.Scale(grams);
        return dto;
    }

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
        double MatchQuality
    );

    private sealed record AggregateResult(double Value, double NormalizedMad);
    private sealed record SourceCandidates(List<Candidate> Candidates, long LatencyMs);
    private sealed record SourceSettings(string Name, bool Enabled, int FailureThreshold, TimeSpan Cooldown);

    private sealed record ConsensusFood(
        string Name,
        string Brand,
        double Calories,
        double Carbohydrates,
        double Fat,
        double Proteins,
        double Confidence,
        string[] Sources,
        double Relevance
    );
}

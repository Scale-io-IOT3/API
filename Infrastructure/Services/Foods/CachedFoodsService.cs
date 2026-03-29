using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.Interface;
using Core.Interface.Foods;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Infrastructure.Services.Foods.Abstract;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Foods;

public class CachedFoodsService<T>(IClient<T> client, IMemoryCache cache, ILogger<CachedFoodsService<T>> logger)
    : FoodsService where T : IResponse
{
    private static long _totalRequests;
    private static long _cacheHits;
    private static long _emptyResponses;

    private readonly MemoryCacheEntryOptions _sourceCacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };

    public override async Task<FoodResponse?> FetchAsync(string input, double? grams)
    {
        var normalized = NormalizeQuery(input);
        if (!IsValidQuery(normalized))
        {
            logger.LogInformation(
                "Food query rejected. service={Service}, normalized_query='{Query}', min_query_length={MinLength}",
                typeof(T).Name,
                normalized,
                MinQueryLength
            );
            return new FoodResponse { Foods = [] };
        }

        Interlocked.Increment(ref _totalRequests);
        var key = CacheKey(normalized);
        var cacheHit = cache.TryGetValue(key, out T? source) && source is not null;
        long sourceLatencyMs = 0;

        if (!cacheHit)
        {
            var stopwatch = Stopwatch.StartNew();
            source = await client.Fetch(normalized);
            stopwatch.Stop();
            sourceLatencyMs = stopwatch.ElapsedMilliseconds;

            if (source is not null)
            {
                cache.Set(key, source, _sourceCacheOptions);
            }
        }
        else
        {
            Interlocked.Increment(ref _cacheHits);
        }

        var gramsValue = GetWeight(grams);
        var response = FoodResponse.From(source, gramsValue, normalized);

        if (response.Foods.Length == 0)
        {
            Interlocked.Increment(ref _emptyResponses);
        }

        var totalRequests = Volatile.Read(ref _totalRequests);
        var cacheHits = Volatile.Read(ref _cacheHits);
        var emptyResponses = Volatile.Read(ref _emptyResponses);
        var hitRate = totalRequests == 0 ? 0 : (double)cacheHits / totalRequests;
        var emptyRate = totalRequests == 0 ? 0 : (double)emptyResponses / totalRequests;

        logger.LogInformation(
            "Food query done. service={Service}, query='{Query}', normalized_query='{NormalizedQuery}', grams={Grams}, cache_hit={CacheHit}, source_latency_ms={LatencyMs}, result_count={Count}, hit_rate={HitRate}, empty_rate={EmptyRate}",
            typeof(T).Name,
            input,
            normalized,
            gramsValue,
            cacheHit,
            sourceLatencyMs,
            response.Foods.Length,
            $"{hitRate:P1}",
            $"{emptyRate:P1}"
        );

        return response;
    }

    protected override Task<FoodResponse?> FetchFromSource(string input, double? grams)
    {
        throw new NotSupportedException("This service uses FetchAsync with source-level caching.");
    }

    protected virtual int MinQueryLength => 1;

    protected virtual bool IsValidQuery(string normalizedQuery)
    {
        return normalizedQuery.Length >= MinQueryLength;
    }

    protected virtual string NormalizeQuery(string input)
    {
        return NormalizeToken(input);
    }

    protected virtual string CacheKey(string normalizedQuery)
    {
        return $"{typeof(T).Name}_{normalizedQuery}";
    }

    protected static double GetWeight(double? weight)
    {
        return weight is null or <= 0 ? 100.0 : weight.Value;
    }

    private static string NormalizeToken(string value)
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
}

public class BarcodeFoodService(
    IClient<BarcodeResponse> client,
    IMemoryCache cache,
    ILogger<CachedFoodsService<BarcodeResponse>> logger
) : CachedFoodsService<BarcodeResponse>(client, cache, logger), IBarcodeService;

public class FreshFoodService(
    IClient<FreshFoodResponse> client,
    IMemoryCache cache,
    ILogger<CachedFoodsService<FreshFoodResponse>> logger
) : CachedFoodsService<FreshFoodResponse>(client, cache, logger), IFreshFoodsService
{
    protected override int MinQueryLength => 2;
}

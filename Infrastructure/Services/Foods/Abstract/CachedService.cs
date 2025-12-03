using Core.DTO.Foods;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services.Foods.Abstract;

public abstract class CachedService(IMemoryCache cache) : FoodsService
{
    private readonly MemoryCacheEntryOptions _options = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };

    public override async Task<FoodResponse?> FetchAsync(string input, double? grams)
    {
        var key = CacheKey(input, grams);
        if (cache.TryGetValue(key, out FoodResponse? cached)) return cached;

        var response = await base.FetchAsync(input, grams);
        cache.Set(key, response, _options);

        return response;
    }

    protected virtual string CacheKey(string input, double? w)
    {
        return $"{input.Trim().ToLowerInvariant()}_{GetWeight(w)}";
    }

    protected static double GetWeight(double? weight)
    {
        return weight is null or <= 0 ? 100.0 : weight.Value;
    }
}
using Core.DTO.Foods;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services.Abstract;

public abstract class CachedService(IMemoryCache cache) : Service
{
    private readonly MemoryCacheEntryOptions _options = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };

    public override async Task<FoodResponse?> FetchAsync(string input, double? grams)
    {
        var key = Key(input, grams);
        if (cache.TryGetValue(key, out FoodResponse? cached)) return cached;

        var response = await base.FetchAsync(input, grams);
        cache.Set(key, response, _options);

        return response;
    }

    protected virtual string Key(string input, double? w) => $"{input.Trim().ToLowerInvariant()}_{GetWeight(w)}";
    protected static double GetWeight(double? weight) => weight is null or <= 0 ? 100.0 : weight.Value;
}
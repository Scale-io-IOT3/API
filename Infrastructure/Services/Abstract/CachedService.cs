using Core.DTO.Foods;
using Core.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services.Abstract;

public abstract class CachedService(IMemoryCache cache) : IService
{
    private readonly MemoryCacheEntryOptions _options = new()
    {
        SlidingExpiration = TimeSpan.FromHours(1),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
    };

    public async Task<FoodResponse?> FetchAsync(string input, double? grams = null)
    {
        var key = GenerateKey(input, grams);
        if (cache.TryGetValue(key, out FoodResponse? cached)) return cached;

        var response = await Fetch(input, grams);
        cache.Set(key, response, _options);

        return response;
    }

    protected abstract Task<FoodResponse?> Fetch(string input, double? grams);

    protected virtual string GenerateKey(string input, double? grams)
    {
        var weight = grams is null or <= 0 ? 100.0 : grams.Value;
        return $"{input.Trim().ToLowerInvariant()}_{weight}";
    }

    protected static double GetWeight(double? weight) => weight is null or <= 0 ? 100.0 : weight.Value;
}
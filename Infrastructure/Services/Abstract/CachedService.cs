using System.Globalization;
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

    protected virtual string GenerateKey(string input, double? grams)
    {
        var weight = IsValid(grams) ? grams!.Value.ToString(CultureInfo.InvariantCulture) : "100";
        return $"{input.Trim().ToLowerInvariant()}_{weight}";
    }

    protected abstract Task<FoodResponse?> Fetch(string input, double? grams);

    private static bool IsValid(double? g)
    {
        return g is null or > 0;
    }
}
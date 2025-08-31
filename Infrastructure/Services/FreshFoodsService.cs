using Core.DTO.FreshFoods;
using Infrastructure.Clients;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class FreshFoodsService(FoodsClient client, IMemoryCache cache) : CachedService<FoodResponse>(cache)
{
    protected override string GenerateKey(string input, double? grams)
    {
        var weight = IsValid(grams) ? grams!.Value : 100;
        return $"raw_{input.Trim().ToLowerInvariant()}_{weight}";
    }

    protected override async Task<FoodResponse?> Fetch(string input, double? grams)
    {
        var response = await client.FetchFood(input);
        return Scale(response, grams);
    }

    private static FoodResponse? Scale(FoodResponse? response, double? weight)
    {
        if (IsEmpty()) return null;

        var grams = weight is null or <= 0 ? 100.0 : weight.Value;
        return response?.Treat(grams);

        bool IsEmpty()
        {
            return response?.Foods is null || response.Foods.Length == 0;
        }
    }

    private static bool IsValid(double? g)
    {
        return g is null or > 0;
    }
}
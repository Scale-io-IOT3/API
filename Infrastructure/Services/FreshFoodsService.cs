using Core.DTO.FreshFoods;
using Infrastructure.Clients;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class FreshFoodsService(FoodsClient client, IMemoryCache cache) : CachedService<FoodResponse>(cache)
{
    protected override string GenerateKey(string input, double? grams) => $"food_{input.Trim().ToLowerInvariant()}";

    protected override async Task<FoodResponse?> Fetch(string input, double? grams)
    {
        var response = await client.FetchFood(input);
        return response?.Filter();
    }
}
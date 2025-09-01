using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.Interface;
using Infrastructure.Clients;
using Infrastructure.Services.Abstract;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class FreshFoodsService(FoodsClient client, IMemoryCache cache) : CachedService(cache), IFreshFoodsService
{
    protected override string GenerateKey(string input, double? grams) => $"raw_{base.GenerateKey(input, grams)}";
    protected override async Task<FoodResponse?> Fetch(string input, double? grams)
    {
        var response = await client.FetchFood(input);
        return Scale(response, grams);
    }

    private static FoodResponse? Scale(FreshFoodResponse? response, double? weight)
    {
        if (IsEmpty()) return null;

        var grams = weight is null or <= 0 ? 100.0 : weight.Value;
        return FoodResponse.FromFreshFoodResponse(response, grams);

        bool IsEmpty()
        {
            return response?.Foods is null || response.Foods.Length == 0;
        }
    }
}
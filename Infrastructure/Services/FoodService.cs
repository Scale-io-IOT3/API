using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.Interface;
using Infrastructure.Services.Abstract;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class FoodService<TResponse>(IApiClient<TResponse> client, IMemoryCache cache)
    : CachedService(cache) where TResponse : ISourceResponse
{
    protected override string GenerateKey(string input, double? grams) =>
        $"{nameof(TResponse)}_{base.GenerateKey(input, grams)}";

    protected override async Task<FoodResponse?> FetchFromSource(string input, double? grams)
    {
        var response = await client.Fetch(input);
        return Scale(response, grams);
    }

    private static FoodResponse Scale(TResponse? response, double? weight)
    {
        var grams = GetWeight(weight);
        return FoodResponse.From(response, grams);
    }
}

public class BarcodeFoodService(IApiClient<BarcodeResponse> client, IMemoryCache cache)
    : FoodService<BarcodeResponse>(client, cache), IBarcodeService;

public class FreshFoodService(IApiClient<FreshFoodResponse> client, IMemoryCache cache)
    : FoodService<FreshFoodResponse>(client, cache), IFreshFoodsService;
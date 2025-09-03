using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.Interface;
using Infrastructure.Services.Abstract;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class FoodService<T>(IClient<T> client, IMemoryCache cache) : CachedService(cache) where T : IResponse
{
    protected override async Task<FoodResponse?> FetchFromSource(string input, double? grams)
    {
        var response = await client.Fetch(input);
        return Scale(response, grams);
    }

    protected override string CacheKey(string input, double? w) => $"{nameof(T)}_{base.CacheKey(input, w)}";
    private static FoodResponse Scale(T? response, double? w) => FoodResponse.From(response, GetWeight(w));
}

public class BarcodeFoodService(IClient<BarcodeResponse> client, IMemoryCache cache)
    : FoodService<BarcodeResponse>(client, cache), IBarcodeService;

public class FreshFoodService(IClient<FreshFoodResponse> client, IMemoryCache cache)
    : FoodService<FreshFoodResponse>(client, cache), IFreshFoodsService;
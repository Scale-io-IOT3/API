using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.Interface;
using Core.Interface.Foods;
using Infrastructure.Services.Foods.Abstract;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services.Foods;

public class CachedFoodsService<T>(IClient<T> client, IMemoryCache cache) : CachedService(cache) where T : IResponse
{
    protected override async Task<FoodResponse?> FetchFromSource(string input, double? grams)
    {
        var response = await client.Fetch(input);
        return Scale(response, grams);
    }

    protected override string CacheKey(string input, double? w)
    {
        return $"{nameof(T)}_{base.CacheKey(input, w)}";
    }

    private static FoodResponse Scale(T? response, double? w)
    {
        return FoodResponse.From(response, GetWeight(w));
    }
}

public class BarcodeFoodService(IClient<BarcodeResponse> client, IMemoryCache cache)
    : CachedFoodsService<BarcodeResponse>(client, cache), IBarcodeService;

public class FreshFoodService(IClient<FreshFoodResponse> client, IMemoryCache cache)
    : CachedFoodsService<FreshFoodResponse>(client, cache), IFreshFoodsService;
using Core.DTO.Foods;
using Core.Interface;
using Infrastructure.Clients;
using Infrastructure.Services.Abstract;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class BarcodeService(BarcodeClient client, IMemoryCache cache) : CachedService(cache), IBarcodeService
{
    protected override string GenerateKey(string input, double? grams) => $"code_{base.GenerateKey(input, grams)}";

    protected override async Task<FoodResponse?> Fetch(string input, double? grams)
    {
        var response = await client.FetchProduct(input);
        return ScaleProduct(
            FoodResponse.FromBarcodeResponse(response), grams
        );
    }

    private static FoodResponse? ScaleProduct(FoodResponse? response, double? weight)
    {
        if (response?.Foods is null) return null;

        var grams = weight is null or <= 0 ? 100.0 : weight.Value;
        response.Foods.First().Scale(grams);
        return response;
    }
}
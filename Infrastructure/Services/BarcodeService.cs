using Core.DTO;
using Infrastructure.Clients;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public class BarcodeService(BarcodeClient client, IMemoryCache cache) : CachedService<BarcodeResponse>(cache)
{
    protected override string GenerateKey(string input, double? grams)
    {
        var weight = IsValid(grams) ? grams!.Value : 100;
        return $"code_{input.Trim().ToLowerInvariant()}_{weight}";
    }

    protected override async Task<BarcodeResponse?> Fetch(string input, double? grams)
    {
        var response = await client.FetchProduct(input);
        return ScaleProduct(response, grams);
    }

    private static BarcodeResponse? ScaleProduct(BarcodeResponse? response, double? weight)
    {
        if (response?.Product is null) return null;

        var grams = weight is null or <= 0 ? 100.0 : weight.Value;
        response.Product.ScaleNutriments(grams);
        return response;
    }

    private static bool IsValid(double? g)
    {
        return g is null or > 0;
    }
}
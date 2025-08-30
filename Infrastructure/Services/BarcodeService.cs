using Core.DTO;
using Core.Interface;
using Infrastructure.Clients;

namespace Infrastructure.Services;

public class BarcodeService(BarcodeClient client) : IBarcodeService
{
    public async Task<BarcodeResponse?> FetchProduct(string code, double grams)
    {
        var response = await client.FetchProduct(code);
        return response?.Product is null ? null : ScaleProduct(response, grams);
    }

    private static BarcodeResponse ScaleProduct(BarcodeResponse response, double weight)
    {
        if (weight <= 0) weight = 100.0;
        response.Product.ScaleNutriments(weight);

        return response;
    }
}
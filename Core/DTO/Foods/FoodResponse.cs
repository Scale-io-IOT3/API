using System.Text.Json.Serialization;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.Interface;

namespace Core.DTO.Foods;

public class FoodResponse : IResponse
{
    private const int MaxFreshResults = 10;
    [JsonPropertyName("foods")] public required FoodDto[] Foods { get; set; }


    private static FoodResponse FromBarcodeResponse(BarcodeResponse? response, double grams)
    {
        return response?.Product == null ? Empty() : FromFoods([FoodDto.FromProduct(response.Product)], grams);
    }

    private static FoodResponse FromFreshFoodResponse(FreshFoodResponse? response, double grams, string query)
    {
        if (response?.Foods == null || response.Foods.Length == 0) return Empty();

        var foods = response.FilterAndRank(query, MaxFreshResults)
            .Foods
            .Select(FoodDto.FromFreshFood)
            .ToArray();

        return FromFoods(foods, grams);
    }

    public static FoodResponse From<T>(T? response, double grams, string? query = null)
    {
        return response switch
        {
            BarcodeResponse barcode => FromBarcodeResponse(barcode, grams),
            FreshFoodResponse fresh => FromFreshFoodResponse(fresh, grams, query ?? string.Empty),
            null => new FoodResponse { Foods = [] },
            _ => throw new ArgumentException($"Unsupported response type: {nameof(IResponse)}")
        };
    }


    private static FoodResponse FromFoods(FoodDto[] foods, double grams)
    {
        foreach (var f in foods) f.Scale(grams);
        return new FoodResponse { Foods = foods };
    }

    private static FoodResponse Empty()
    {
        return new FoodResponse { Foods = [] };
    }
}

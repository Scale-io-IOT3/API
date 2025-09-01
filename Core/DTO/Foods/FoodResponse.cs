using System.Text.Json.Serialization;
using Core.DTO.FreshFoods;

namespace Core.DTO.Foods;

public class FoodResponse
{
    [JsonPropertyName("foods")] public Food[] Foods { get; set; }

    public static FoodResponse FromBarcodeResponse(BarcodeResponse? response)
    {
        if (response?.Product == null) return new FoodResponse { Foods = [] };

        var food = Food.FromProduct(response.Product);
        return new FoodResponse { Foods = [food] };
    }

    public static FoodResponse FromFreshFoodResponse(FreshFoodResponse? response, double grams)
    {
        if (response?.Foods == null || response.Foods.Length == 0) return new FoodResponse { Foods = [] };

        var foods = response.Filter()
            .Foods
            .Select(Food.FromFreshFood)
            .ToArray();

        foreach (var f in foods) f.Scale(grams);

        return new FoodResponse { Foods = foods };
    }
}
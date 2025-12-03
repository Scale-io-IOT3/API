using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.FreshFoods;

public class FreshFoodResponse : IResponse
{
    [JsonPropertyName("foods")] public required FreshFood[] Foods { get; init; }

    public FreshFoodResponse Filter()
    {
        var filtered = Foods
            .Select(f => new FreshFood
            {
                Description = f.Description,
                Category = f.Category,
                FoodNutrients = f.GetMacros()
            })
            .ToArray();

        return new FreshFoodResponse { Foods = filtered };
    }
}
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Core.Interface;

namespace Core.DTO.FreshFoods;

public partial class FreshFoodResponse : IResponse
{
    [JsonPropertyName("foods")] public FreshFood[] Foods { get; init; }

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
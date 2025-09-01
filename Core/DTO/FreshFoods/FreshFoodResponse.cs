using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Core.DTO.FreshFoods;

public partial class FreshFoodResponse
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

        return new FreshFoodResponse { Foods = filtered }.GetRaw();
    }


    private FreshFoodResponse GetRaw()
    {
        var filtered = Foods.Where(f => Raw().IsMatch(f.Description)).ToArray();

        return new FreshFoodResponse
        {
            Foods = filtered
        };
    }


    [GeneratedRegex(@"\braw\b", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex Raw();
}
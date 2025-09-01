using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Core.DTO.FreshFoods;

public partial class FreshFoodResponse
{
    [JsonPropertyName("foods")] public FreshFood[] Foods { get; init; }

    private FreshFoodResponse Filter()
    {
        var filtered = Foods.Where(f => Raw().IsMatch(f.Description)).ToArray();

        return new FreshFoodResponse
        {
            Foods = filtered
        };
    }

    public FreshFoodResponse Process(double grams)
    {
        var res = Filter();
        var foods = res.Foods.Select(f => f.Process(grams)).ToArray();

        return new FreshFoodResponse { Foods = foods };
    }


    [GeneratedRegex(@"\braw\b", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex Raw();
}
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Core.DTO.FreshFoods;

public partial class FoodResponse
{
    [JsonPropertyName("foods")] public Food[] Foods { get; init; }

    private FoodResponse Filter()
    {
        var filtered = Foods.Where(f => Raw().IsMatch(f.Description)).ToArray();

        return new FoodResponse
        {
            Foods = filtered
        };
    }

    public FoodResponse Treat(double grams)
    {
        var res = Filter();
        return new FoodResponse
        {
            Foods = res.Foods
                .Select(f => f.Treat(grams))
                .ToArray()
            
        };
    }

    [GeneratedRegex(@"\braw\b", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex Raw();
}

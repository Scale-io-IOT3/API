using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Core.DTO.FreshFoods;

public partial class FoodResponse
{
    [JsonPropertyName("foods")] public Food[] Foods { get; init; }

    public FoodResponse Filter()
    {
        var filtered = Foods.Where(f => Raw().IsMatch(f.Description)).ToArray();

        return new FoodResponse
        {
            Foods = filtered,
        };
    }

    [GeneratedRegex(@"\braw\b", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex Raw();
}
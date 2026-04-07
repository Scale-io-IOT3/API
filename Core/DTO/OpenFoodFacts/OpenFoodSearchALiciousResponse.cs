using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.OpenFoodFacts;

public class OpenFoodSearchALiciousResponse : IResponse
{
    [JsonPropertyName("hits")] public OpenFoodSearchALiciousHit[] Hits { get; init; } = [];
}

public class OpenFoodSearchALiciousHit
{
    [JsonPropertyName("product_name")] public string? ProductName { get; init; }
    [JsonPropertyName("product_name_en")] public string? ProductNameEn { get; init; }
    [JsonPropertyName("product_name_fr")] public string? ProductNameFr { get; init; }
    [JsonPropertyName("brands")] public JsonElement Brands { get; init; }
    [JsonPropertyName("nutriscore_grade")] public string? NutriScoreGrade { get; init; }
    [JsonPropertyName("nutrition_grades")] public string? NutritionGrades { get; init; }
    [JsonPropertyName("nutrition_grade_fr")] public string? NutritionGradeFr { get; init; }
    [JsonPropertyName("nutrition_grades_tags")] public string[]? NutritionGradesTags { get; init; }
    [JsonPropertyName("nutrient_levels")] public Dictionary<string, string>? NutrientLevels { get; init; }

    [JsonIgnore] public string ResolvedName => FirstNonEmpty(ProductName, ProductNameEn, ProductNameFr);
    [JsonIgnore] public string ResolvedBrands => ResolveBrands(Brands);

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string ResolveBrands(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?.Trim() ?? string.Empty;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var brands = value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();

        return brands.Length == 0 ? string.Empty : string.Join(", ", brands);
    }
}

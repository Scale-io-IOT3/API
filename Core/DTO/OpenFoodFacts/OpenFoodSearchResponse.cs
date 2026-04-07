using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.OpenFoodFacts;

public class OpenFoodSearchResponse : IResponse
{
    [JsonPropertyName("products")] public OpenFoodSearchProduct[] Products { get; init; } = [];
}

public class OpenFoodSearchProduct
{
    [JsonPropertyName("product_name")] public string? Name { get; init; }
    [JsonPropertyName("brands")] public string? Brands { get; init; }
    [JsonPropertyName("nutriscore_grade")] public string? NutriScoreGrade { get; init; }
    [JsonPropertyName("nutrition_grades")] public string? NutritionGrades { get; init; }
    [JsonPropertyName("nutrition_grade_fr")] public string? NutritionGradeFr { get; init; }
    [JsonPropertyName("nutrition_grades_tags")] public string[]? NutritionGradesTags { get; init; }
    [JsonPropertyName("nutrient_levels")] public Dictionary<string, string>? NutrientLevels { get; init; }
    [JsonPropertyName("nutriments")] public OpenFoodNutriments? Nutriments { get; init; }
}

public class OpenFoodNutriments
{
    [JsonPropertyName("energy-kcal_100g")] public double? EnergyKcal100g { get; init; }
    [JsonPropertyName("carbohydrates_100g")] public double? Carbohydrates100g { get; init; }
    [JsonPropertyName("fat_100g")] public double? Fat100g { get; init; }
    [JsonPropertyName("proteins_100g")] public double? Proteins100g { get; init; }
}

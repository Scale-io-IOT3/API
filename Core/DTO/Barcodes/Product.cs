using System.Text.Json.Serialization;
using Core.DTO.Foods;

namespace Core.DTO.Barcodes;

public class Product
{
    [JsonPropertyName("brands")] public string? Brands { get; set; }
    [JsonPropertyName("product_name")] public string? Name { get; set; }
    [JsonPropertyName("product_name_en")] public string? NameEn { get; set; }
    [JsonPropertyName("product_name_fr")] public string? NameFr { get; set; }
    [JsonPropertyName("generic_name")] public string? GenericName { get; set; }
    [JsonPropertyName("generic_name_en")] public string? GenericNameEn { get; set; }
    [JsonPropertyName("nutriscore_grade")] public string? NutriScoreGrade { get; set; }
    [JsonPropertyName("nutrition_grades")] public string? NutritionGrades { get; set; }
    [JsonPropertyName("nutrition_grade_fr")] public string? NutritionGradeFr { get; set; }
    [JsonPropertyName("nutrition_grades_tags")] public string[]? NutritionGradesTags { get; set; }
    [JsonPropertyName("nutriments")] public BarcodeNutriments? Nutriments { get; set; }

    [JsonIgnore] public string ResolvedBrand => Brands?.Trim() ?? string.Empty;

    [JsonIgnore]
    public string ResolvedName => FirstNonEmpty(Name, NameEn, NameFr, GenericName, GenericNameEn);

    [JsonIgnore] public MacrosDto ResolvedMacros => Nutriments?.ToMacrosDto() ?? MacrosDto.From(0, 0, 0, 0);
    [JsonIgnore]
    public string? ResolvedNutritionGrade => NutritionGrade.Normalize(
        NutriScoreGrade,
        NutritionGrades,
        NutritionGradeFr,
        NutritionGradesTags?.FirstOrDefault()
    );

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
}

public class BarcodeNutriments
{
    [JsonPropertyName("energy-kcal_100g")] public double? EnergyKcal100g { get; init; }
    [JsonPropertyName("energy-kcal")] public double? EnergyKcal { get; init; }
    [JsonPropertyName("energy-kcal_value_computed")] public double? EnergyKcalComputed { get; init; }

    [JsonPropertyName("carbohydrates_100g")] public double? Carbohydrates100g { get; init; }
    [JsonPropertyName("carbohydrates")] public double? Carbohydrates { get; init; }

    [JsonPropertyName("fat_100g")] public double? Fat100g { get; init; }
    [JsonPropertyName("fat")] public double? Fat { get; init; }

    [JsonPropertyName("proteins_100g")] public double? Proteins100g { get; init; }
    [JsonPropertyName("proteins")] public double? Proteins { get; init; }

    public MacrosDto ToMacrosDto()
    {
        var carbs = Carbohydrates100g ?? Carbohydrates ?? 0;
        var fat = Fat100g ?? Fat ?? 0;
        var proteins = Proteins100g ?? Proteins ?? 0;
        var calories = EnergyKcal100g ?? EnergyKcal ?? EnergyKcalComputed ?? carbs * 4 + fat * 9 + proteins * 4;

        return MacrosDto.From(carbs, fat, proteins, (int)Math.Round(calories));
    }
}

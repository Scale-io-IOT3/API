using System.Text.Json.Serialization;
using Core.DTO.FreshFoods;

namespace Core.DTO;

public class MacroPercentage
{
    private const int CalsPerGramCarb = 4;
    private const int CalsPerGramProtein = 4;
    private const int CalsPerGramFat = 9;

    [JsonPropertyName("carbohydrates_%")] public double CarbsPct { get; init; }
    [JsonPropertyName("fat_%")] public double FatPct { get; init; }
    [JsonPropertyName("proteins_%")] public double ProteinsPct { get; init; }

    public static MacroPercentage FromNutrients(Nutrient[] nutrients)
    {
        var protein = nutrients
            .FirstOrDefault(n => n.NutrientName.Equals("Protein", StringComparison.OrdinalIgnoreCase))?.Value ?? 0;
        var fat = nutrients
            .FirstOrDefault(n => n.NutrientName.Equals("Total lipid (fat)", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? 0;
        var carbs = nutrients.FirstOrDefault(n =>
            n.NutrientName.Equals("Carbohydrate, by difference", StringComparison.OrdinalIgnoreCase))?.Value ?? 0;
        var calories =
            nutrients.FirstOrDefault(n => n.NutrientName.Equals("Energy", StringComparison.OrdinalIgnoreCase))
                ?.Value ??
            protein * CalsPerGramProtein + fat * CalsPerGramFat + carbs * CalsPerGramCarb;

        if (calories == 0) calories = 1;

        return new MacroPercentage
        {
            ProteinsPct = Math.Round(protein * CalsPerGramProtein / calories * 100, 2),
            FatPct = Math.Round(fat * CalsPerGramFat / calories * 100, 2),
            CarbsPct = Math.Round(carbs * CalsPerGramCarb / calories * 100, 2)
        };
    }

    public static MacroPercentage FromNutriment(Nutriments nutriments)
    {
        var proteinKcal = nutriments.Proteins * CalsPerGramProtein;
        var fatKcal = nutriments.Fat * CalsPerGramFat;
        var carbKcal = nutriments.Carbohydrates * CalsPerGramCarb;

        return new MacroPercentage
        {
            ProteinsPct = Math.Round(proteinKcal / nutriments.Calories * 100, 2),
            FatPct = Math.Round(fatKcal / nutriments.Calories * 100, 2),
            CarbsPct = Math.Round(carbKcal / nutriments.Calories * 100, 2)
        };
    }
}
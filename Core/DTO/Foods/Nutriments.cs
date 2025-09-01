using System.Text.Json.Serialization;
using Core.DTO.FreshFoods;

namespace Core.DTO;

public class Nutriments
{
    [JsonPropertyName("energy-kcal_value_computed")]
    public double E { private get; init; }

    [JsonPropertyName("calories")] public int Calories => (int)Math.Round(E);

    [JsonPropertyName("carbohydrates")] public double Carbohydrates { get; init; }
    [JsonPropertyName("fat")] public double Fat { get; init; }
    [JsonPropertyName("proteins")] public double Proteins { get; init; }


    private double MacroPercentage(double grams, int calGram) => E > 0 ? Math.Round(grams * calGram / E * 100, 1) : 0;

    public Nutriments ForAmount(double grams)
    {
        var factor = grams / 100.0;
        return new Nutriments
        {
            E = Math.Round(E * factor, 1),
            Carbohydrates = Math.Round(Carbohydrates * factor, 2),
            Fat = Math.Round(Fat * factor, 2),
            Proteins = Math.Round(Proteins * factor, 2),
        };
    }
    
    public static Nutriments FromNutrients(Nutrient[] nutrients)
    {
        return new Nutriments
        {
            Proteins = nutrients
                .FirstOrDefault(n => n.NutrientName.Equals("Protein", StringComparison.OrdinalIgnoreCase))?.Value ?? 0,
            Fat = nutrients
                .FirstOrDefault(n => n.NutrientName.Equals("Total lipid (fat)", StringComparison.OrdinalIgnoreCase))
                ?.Value ?? 0,
            Carbohydrates = nutrients.FirstOrDefault(n =>
                n.NutrientName.Equals("Carbohydrate, by difference", StringComparison.OrdinalIgnoreCase))?.Value ?? 0,
            E = nutrients
                .FirstOrDefault(n => n.NutrientName.Equals("Energy", StringComparison.OrdinalIgnoreCase))?.Value ?? 0
        };
    }
}
using System.Text.Json.Serialization;
using Core.DTO.Barcodes;

namespace Core.DTO.FreshFoods;

public class FreshFood
{
    private static readonly HashSet<string> Macros = new(StringComparer.OrdinalIgnoreCase)
    {
        "Protein",
        "Total lipid (fat)",
        "Carbohydrate, by difference",
        "Energy"
    };

    [JsonPropertyName("description")] public string Description { get; init; }

    [JsonPropertyName("foodNutrients")] public Nutrient[] FoodNutrients { get; set; }

    public FreshFood Process(double grams)
    {
        return Filter().ForAmount(grams);
    }

    private FreshFood Filter()
    {
        return new FreshFood
        {
            Description = Description,
            FoodNutrients = (FoodNutrients ?? [])
                .Where(n =>
                    Macros.Any(m => n.NutrientName.Contains(m, StringComparison.OrdinalIgnoreCase)) &&
                    (!n.NutrientName.Equals("Energy", StringComparison.OrdinalIgnoreCase)
                     || n.UnitName.Equals("KCAL", StringComparison.OrdinalIgnoreCase))
                )
                .ToArray()
        };
    }

    private FreshFood ForAmount(double grams)
    {
        if (grams is <= 0 or 100) return this;

        var scaledNutrients = FoodNutrients
            .Select(n => n.ForAmount(grams))
            .ToArray();

        return new FreshFood
        {
            Description = Description,
            FoodNutrients = scaledNutrients
        };
    }
}
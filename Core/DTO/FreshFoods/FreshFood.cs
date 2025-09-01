using System.Text.Json.Serialization;

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
    [JsonPropertyName("foodCategory")] public string Category { get; init; }
    [JsonPropertyName("foodNutrients")] public Nutrient[] FoodNutrients { get; set; }

    public Nutrient[] GetMacros()
    {
        return FoodNutrients.Where(IsMacro).ToArray();
    }

    private static bool IsMacro(Nutrient n)
    {
        if (Macros.Any(m => n.NutrientName.Contains(m, StringComparison.OrdinalIgnoreCase)))
            return true;

        return n.NutrientName.Equals("Energy", StringComparison.OrdinalIgnoreCase)
               && n.UnitName.Equals("KCAL", StringComparison.OrdinalIgnoreCase);
    }
}
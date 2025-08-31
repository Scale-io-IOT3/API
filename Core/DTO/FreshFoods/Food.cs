using System.Text.Json.Serialization;

namespace Core.DTO.FreshFoods;

public class Food
{
    [JsonPropertyName("description")] public string Description { get; init; }

    [JsonPropertyName("dataType")] public string DataType { private get; init; }

    [JsonPropertyName("foodNutrients")] public FoodNutrient[] FoodNutrients { get; set; }

    private Food ForAmount(double grams)
    {
        return new Food
        {
            Description = Description,
            DataType = DataType,
            FoodNutrients = FoodNutrients.Select(n => n.ForAmount(grams)).ToArray()
        };
    }

    private Food Filter()
    {
        string[] macros = ["Protein", "Total lipid (fat)", "Carbohydrate, by difference", "Energy"];

        var filtered = FoodNutrients
            .Where(n => macros.Any(m => n.NutrientName.Contains(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var cleaned = GetCalories(filtered);

        return new Food
        {
            Description = Description,
            DataType = DataType,
            FoodNutrients = cleaned,
        };
    }


    private static FoodNutrient[] GetCalories(List<FoodNutrient> filtered)
    {
        var energyKcal = filtered.FirstOrDefault(n =>
            n.NutrientName.Equals("Energy", StringComparison.OrdinalIgnoreCase) &&
            n.UnitName.Equals("KCAL", StringComparison.OrdinalIgnoreCase));

        if (energyKcal is not null)
        {
            filtered.RemoveAll(n =>
                n.NutrientName.Equals("Energy", StringComparison.OrdinalIgnoreCase) && n.UnitName != "KCAL");
        }

        return filtered.ToArray();
    }


    public Food Treat(double g)
    {
        var filtered = Filter();
        return filtered.ForAmount(g);
    }
}
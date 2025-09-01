using Core.DTO.FreshFoods;
using Core.Interface;

namespace Core.DTO.Foods;

public class NutrientsMapper : IMacroSource
{
    public NutrientsMapper(Nutrient[] nutrients)
    {
        Carbohydrates = nutrients
            .FirstOrDefault(n =>
                n.NutrientName.Equals("Carbohydrate, by difference", StringComparison.OrdinalIgnoreCase))?.Value ?? 0;
        Fat = nutrients
            .FirstOrDefault(n => n.NutrientName.Equals("Total lipid (fat)", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? 0;
        Proteins = nutrients
            .FirstOrDefault(n => n.NutrientName.Equals("Protein", StringComparison.OrdinalIgnoreCase))?.Value ?? 0;

        Calories = (int)(nutrients.FirstOrDefault(n =>
            n.NutrientName.Equals("Energy", StringComparison.OrdinalIgnoreCase) &&
            n.UnitName.Equals("Kcal", StringComparison.OrdinalIgnoreCase)
        )?.Value ?? Proteins * 4 + Fat * 9 + Carbohydrates * 4);
    }

    public int Calories { get; }
    public double Carbohydrates { get; }
    public double Fat { get; }
    public double Proteins { get; }
}
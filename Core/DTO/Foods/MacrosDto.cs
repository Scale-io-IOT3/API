using System.Text.Json.Serialization;
using Core.Interface.Foods;
using Core.Models.Entities;

namespace Core.DTO.Foods;

public class MacrosDto : IMacroSource
{
    [JsonPropertyName("energy-kcal_value_computed")]
    public double E { private get; init; }

    [JsonPropertyName("percentages")] public Percentage Percentages => Percentage.From(this);
    [JsonIgnore] public int Calories => (int)Math.Round(E);
    [JsonPropertyName("carbohydrates")] public double Carbohydrates { get; init; }
    [JsonPropertyName("fat")] public double Fat { get; init; }
    [JsonPropertyName("proteins")] public double Proteins { get; init; }

    private static double R(double value)
    {
        return Math.Round(value, 1);
    }

    public MacrosDto For(double grams)
    {
        var factor = grams / 100.0;
        return new MacrosDto
        {
            E = R(E * factor),
            Carbohydrates = R(Carbohydrates * factor),
            Fat = R(Fat * factor),
            Proteins = R(Proteins * factor)
        };
    }

    public static MacrosDto From(IMacroSource source)
    {
        return new MacrosDto
        {
            E = R(source.Calories),
            Carbohydrates = R(source.Carbohydrates),
            Fat = R(source.Fat),
            Proteins = R(source.Proteins)
        };
    }

    public List<Macros> ToEntityMacros(string foodId)
    {
        return
        [
            new Macros
            {
                FoodId = foodId,
                MacroTypeId = 1,
                Amount = Carbohydrates,
                Percentage = Percentages.CarbsPct
            },

            new Macros
            {
                FoodId = foodId,
                MacroTypeId = 2,
                Amount = Fat,
                Percentage = Percentages.FatPct
            },

            new Macros
            {
                FoodId = foodId,
                MacroTypeId = 3,
                Amount = Proteins,
                Percentage = Percentages.ProteinsPct
            }
        ];
    }

    public static MacrosDto From(ICollection<Macros> macros, int? calories)
    {
        double carbs = 0;
        double fat = 0;
        double proteins = 0;

        foreach (var m in macros)
            switch (m.MacroTypeId)
            {
                case 1:
                    carbs = m.Amount;
                    break;

                case 2:
                    fat = m.Amount;
                    break;

                case 3:
                    proteins = m.Amount;
                    break;
            }

        var kcal = calories ?? carbs * 4 + fat * 9 + proteins *4;

        return new MacrosDto
        {
            Carbohydrates = carbs,
            Fat = fat,
            Proteins = proteins,
            E = kcal
        };
    }
}
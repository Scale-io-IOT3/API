using System.Text.Json.Serialization;
using Core.Interface.Foods;

namespace Core.DTO.Foods;

public class MacrosDto : IMacroSource
{
    [JsonPropertyName("energy-kcal_value_computed")]
    public double E { private get; init; }

    [JsonPropertyName("percentages")] public Percentage Percentages => Percentage.From(this);
    [JsonPropertyName("calories")] public int Calories => (int)Math.Round(E);
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

    public static MacrosDto From(double carbohydrates, double fat, double proteins, int? calories = null)
    {
        var kcal = calories ?? carbohydrates * 4 + fat * 9 + proteins * 4;

        return new MacrosDto
        {
            Carbohydrates = carbohydrates,
            Fat = fat,
            Proteins = proteins,
            E = R(kcal)
        };
    }
}

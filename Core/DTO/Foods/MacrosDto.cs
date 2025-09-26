using System.Text.Json.Serialization;
using Core.Interface.Foods;

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
}
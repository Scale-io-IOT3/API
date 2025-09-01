using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.Foods;

public class Macros : IMacroSource
{
    [JsonPropertyName("energy-kcal_value_computed")]
    public double E { private get; init; }

    [JsonPropertyName("calories")] public int Calories => (int)Math.Round(E);

    [JsonPropertyName("carbohydrates")] public double Carbohydrates { get; init; }
    [JsonPropertyName("fat")] public double Fat { get; init; }
    [JsonPropertyName("proteins")] public double Proteins { get; init; }

    private static double R(double value) => Math.Round(value, 1);

    public Macros For(double grams)
    {
        var factor = grams / 100.0;
        return new Macros
        {
            E = R(E * factor),
            Carbohydrates = R(Carbohydrates * factor),
            Fat = R(Fat * factor),
            Proteins = R(Proteins * factor)
        };
    }

    public static Macros From(IMacroSource source) => new()
    {
        E = R(source.Calories),
        Carbohydrates = R(source.Carbohydrates),
        Fat = R(source.Fat),
        Proteins = R(source.Proteins)
    };
}
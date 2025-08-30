using System.Text.Json.Serialization;

namespace Core.DTO;

public class BarcodeNutriments
{
    private const int CalsPerGramCarb = 4;
    private const int CalsPerGramProtein = 4;
    private const int CalsPerGramFat = 9;


    [JsonPropertyName("energy-kcal_value_computed")]
    public double E { private get; init; }
    [JsonPropertyName("calories")] public int Calories => (int)Math.Round(E);

    [JsonPropertyName("carbohydrates")] public double Carbohydrates { get; init; }
    [JsonPropertyName("fat")] public double Fat { get; init; }
    [JsonPropertyName("proteins")] public double Proteins { get; init; }

    [JsonPropertyName("carbohydrates_%")] public double CarbsPct => MacroPercentage(Carbohydrates, CalsPerGramCarb);
    [JsonPropertyName("fat_%")] public double FatPct => MacroPercentage(Fat, CalsPerGramFat);
    [JsonPropertyName("proteins_%")] public double ProteinsPct => MacroPercentage(Proteins, CalsPerGramProtein);

    private double MacroPercentage(double grams, int calGram) => E > 0 ? Math.Round((grams * calGram / E) * 100, 1) : 0;
    public BarcodeNutriments ForAmount(double grams)
    {
        var factor = grams / 100.0;
        return new BarcodeNutriments
        {
            E = Math.Round(E * factor, 1),
            Carbohydrates = Math.Round(Carbohydrates * factor, 2),
            Fat = Math.Round(Fat * factor, 2),
            Proteins = Math.Round(Proteins * factor, 2),
        };
    }
}
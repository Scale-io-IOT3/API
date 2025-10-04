using System.Text.Json.Serialization;
using Core.Interface.Foods;

namespace Core.DTO.Foods;

public class Percentage
{
    private const int CalsPerGramCarb = 4;
    private const int CalsPerGramProtein = 4;
    private const int CalsPerGramFat = 9;

    [JsonPropertyName("carbs")] public double CarbsPct { get; init; }
    [JsonPropertyName("fat")] public double FatPct { get; init; }
    [JsonPropertyName("proteins")] public double ProteinsPct { get; init; }

    private static double R(double cal)
    {
        return Math.Round(cal * 100, 1);
    }

    public static Percentage From(IMacroSource source)
    {
        var proteinKcal = source.Proteins * CalsPerGramProtein;
        var fatKcal = source.Fat * CalsPerGramFat;
        var carbKcal = source.Carbohydrates * CalsPerGramCarb;
        var total = proteinKcal + fatKcal + carbKcal;

        if (total == 0)
            return new Percentage
            {
                ProteinsPct = 0,
                FatPct = 0,
                CarbsPct = 0
            };

        return new Percentage
        {
            ProteinsPct = R(proteinKcal / total),
            FatPct = R(fatKcal / total),
            CarbsPct = R(carbKcal / total)
        };
    }
}
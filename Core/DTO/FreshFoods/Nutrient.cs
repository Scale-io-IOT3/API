using System.Text.Json.Serialization;

namespace Core.DTO.FreshFoods;

public class Nutrient
{
    [JsonPropertyName("nutrientName")] public string NutrientName { get; set; }
    [JsonPropertyName("value")] public double Value { get; set; }
    [JsonPropertyName("unitName")] public string UnitName {get; set; }

    public Nutrient ForAmount(double grams)
    {
        var factor = grams / 100.0;
        return new Nutrient
        {
            NutrientName = NutrientName,
            Value = Math.Round(Value * factor, 2),
            UnitName = UnitName
        };
    }
}
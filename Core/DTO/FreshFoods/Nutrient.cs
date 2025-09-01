using System.Text.Json.Serialization;

namespace Core.DTO.FreshFoods;

public class Nutrient
{
    [JsonPropertyName("nutrientName")] public string NutrientName { get; set; }
    [JsonPropertyName("value")] public double Value { get; set; }
    [JsonPropertyName("unitName")] public string UnitName { get; set; }
}
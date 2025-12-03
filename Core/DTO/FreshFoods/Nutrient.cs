using System.Text.Json.Serialization;

namespace Core.DTO.FreshFoods;

public class Nutrient
{
    [JsonPropertyName("nutrientName")] public required string NutrientName { get; set; }
    [JsonPropertyName("value")] public double Value { get; set; }
    [JsonPropertyName("unitName")] public required string UnitName { get; set; }
}
using System.Text.Json.Serialization;

namespace Core.DTO.FreshFoods;

public class Food
{
    [JsonPropertyName("description")] public string Description { get; init; }

    [JsonPropertyName("dataType")] public string DataType { private get; init; }

    [JsonPropertyName("foodNutrients")] public FoodNutrient[] FoodNutrients { get; init; }
}
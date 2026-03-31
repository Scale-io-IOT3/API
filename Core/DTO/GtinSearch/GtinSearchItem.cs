using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.DTO.GtinSearch;

public class GtinSearchItem
{
    [JsonPropertyName("gtin14")] public string? Gtin14 { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("brand")] public string? Brand { get; init; }
    [JsonPropertyName("brand_name")] public string? BrandName { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement> Extra { get; init; } = new();
}

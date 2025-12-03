using System.Text.Json.Serialization;
using Core.DTO.Foods;

namespace Core.DTO.Barcodes;

public class Product
{
    [JsonPropertyName("brands")] public required string Brands { get; set; }

    [JsonPropertyName("product_name")] public required string Name { get; set; }

    [JsonPropertyName("nutriments")] public required MacrosDto MacrosDto { get; set; }
}
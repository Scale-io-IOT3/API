using System.Text.Json.Serialization;
using Core.DTO.Foods;

namespace Core.DTO.Barcodes;

public class Product
{
    [JsonPropertyName("brands")] public string Brands { get; set; }

    [JsonPropertyName("product_name")] public string Name { get; set; }

    [JsonPropertyName("image_url")] public Uri ImageUrl { get; set; }
    [JsonPropertyName("nutriments")] public Macros Macros { get; set; }
}
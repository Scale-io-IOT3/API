using System.Text.Json.Serialization;

namespace Core.DTO;

public class Product
{
    [JsonPropertyName("brands")] public string Brands { get; set; }

    [JsonPropertyName("product_name")] public string Name { get; set; }

    [JsonPropertyName("image_url")] public Uri ImageUrl { get; set; }
    [JsonPropertyName("nutriments")] public BarcodeNutriments Nutriments { get; set; }

    [JsonPropertyName("nutriscore_grade")] public string NutriscoreGrade { get; set; }

    public void ScaleNutriments(double weight)
    {
        Nutriments = Nutriments.ForAmount(weight);
    }
}
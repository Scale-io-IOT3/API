using System.Text.Json.Serialization;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;

namespace Core.DTO;

public class Food
{
    [JsonPropertyName("product_name")] public string Name { get; set; }
    [JsonPropertyName("brands")] public string? Brands { get; set; }
    [JsonPropertyName("image_url")] public Uri? ImageUrl { get; set; }
    [JsonPropertyName("nutriments")] public Nutriments Nutriments { get; set; }
    [JsonPropertyName("nutriments_%")] public MacroPercentage MacroPercentages { get; set; }


    public static Food FromFreshFood(FreshFood food)
    {
        return new Food
        {
            Nutriments = Nutriments.FromNutrients(food.FoodNutrients),
            Brands = null,
            ImageUrl = null,
            MacroPercentages = MacroPercentage.FromNutrients(food.FoodNutrients),
            Name = food.Description
        };
    }

    public static Food FromProduct(Product food)
    {
        return new Food
        {
            Brands = food.Brands,
            ImageUrl = food.ImageUrl,
            MacroPercentages = MacroPercentage.FromNutriment(food.Nutriments),
            Name = food.Name,
            Nutriments = food.Nutriments
        };
    }
    
    public void ScaleNutriments(double weight)
    {
        Nutriments = Nutriments.ForAmount(weight);
    }
}
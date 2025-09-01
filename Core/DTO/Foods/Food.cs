using System.Text.Json.Serialization;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;

namespace Core.DTO.Foods;

public class Food
{
    [JsonPropertyName("product_name")] public string Name { get; set; }
    [JsonPropertyName("brands")] public string? Brands { get; private set; }
    [JsonPropertyName("image_url")] public Uri? ImageUrl { get; set; }
    [JsonPropertyName("nutriments")] public Macros Macros { get; set; }
    [JsonPropertyName("nutriments_%")] public MacroPercentage MacroPercentages { get; set; }


    public static Food FromFreshFood(FreshFood food)
    {
        var source = new NutrientsMapper(food.FoodNutrients);
        return new Food
        {
            Macros = Macros.From(source),
            ImageUrl = null,
            MacroPercentages = MacroPercentage.From(source),
            Name = food.Description,
            Brands = food.Category
        };
    }

    public static Food FromProduct(Product food)
    {
        var macros = food.Macros;
        return new Food
        {
            Brands = food.Brands,
            ImageUrl = food.ImageUrl,
            MacroPercentages = MacroPercentage.From(macros),
            Name = food.Name,
            Macros = food.Macros
        };
    }

    public void Scale(double weight)
    {
        Macros = Macros.For(weight);
    }
}
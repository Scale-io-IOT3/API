using System.Text.Json.Serialization;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;

namespace Core.DTO.Foods;

public class Food
{
    [JsonPropertyName("product_name")] public string HiddenName { private get; set; }
    [JsonPropertyName("nutriments")] public Macros HiddenMacros { private get; set; }
    [JsonPropertyName("name")] public string Name => HiddenName;
    [JsonPropertyName("brands")] public string? Brands { get; private set; }
    [JsonPropertyName("calories")] public int Calories => Macros.Calories;
    [JsonPropertyName("quantity")] public double Quantity { get; private set; }
    [JsonPropertyName("macros")] public Macros Macros => HiddenMacros;

    public static Food FromFreshFood(FreshFood food)
    {
        var source = new NutrientsMapper(food.FoodNutrients);
        return new Food
        {
            HiddenMacros = Macros.From(source),
            HiddenName = food.Description,
            Brands = food.Category
        };
    }

    public static Food FromProduct(Product food)
    {
        return new Food
        {
            Brands = food.Brands,
            HiddenName = food.Name,
            HiddenMacros = food.Macros
        };
    }

    public void Scale(double weight)
    {
        HiddenMacros = HiddenMacros.For(weight);
        Quantity = weight;
    }
}
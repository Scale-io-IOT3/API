using System.Text.Json.Serialization;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.Models.Entities;

namespace Core.DTO.Foods;

public class FoodDto
{
    [JsonPropertyName("product_name")] public required string HiddenName { private get; set; }
    [JsonPropertyName("nutriments")] public required MacrosDto HiddenMacrosDto { private get; set; }
    [JsonPropertyName("name")] public string Name => HiddenName;
    [JsonPropertyName("brands")] public required string Brands { get; set; } = "";
    [JsonPropertyName("calories")] public int Calories => MacrosDto.Calories;
    [JsonPropertyName("quantity")] public double Quantity { get; set; }
    [JsonPropertyName("macros")] public MacrosDto MacrosDto => HiddenMacrosDto;

    public static FoodDto FromFreshFood(FreshFood food)
    {
        var source = new NutrientsMapper(food.FoodNutrients);
        return new FoodDto
        {
            HiddenMacrosDto = MacrosDto.From(source),
            HiddenName = food.Description,
            Brands = food.Category
        };
    }

    public static FoodDto FromProduct(Product food)
    {
        return new FoodDto
        {
            Brands = food.Brands,
            HiddenName = food.Name,
            HiddenMacrosDto = food.MacrosDto
        };
    }

    public void Scale(double weight)
    {
        HiddenMacrosDto = HiddenMacrosDto.For(weight);
        Quantity = weight;
    }

    public Food ToFood()
    {
        var id = Guid.NewGuid().ToString();

        return new Food
        {
            Id = id,
            Name = Name,
            Brands = Brands,
            Calories = Calories,
            Quantity = Quantity,
            Macros = HiddenMacrosDto.ToEntityMacros(id)
        };
    }
}
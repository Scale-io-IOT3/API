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
    [JsonPropertyName("grade")] public string? Grade { get; set; }
    [JsonPropertyName("confidence")] public double? Confidence { get; set; }
    [JsonPropertyName("sources_used")] public string[] SourcesUsed { get; set; } = [];

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
            Brands = food.ResolvedBrand,
            HiddenName = food.ResolvedName,
            HiddenMacrosDto = food.ResolvedMacros,
            Grade = food.ResolvedNutritionGrade
        };
    }

    public void Scale(double weight)
    {
        HiddenMacrosDto = HiddenMacrosDto.For(weight);
        Quantity = weight;
    }

    public Food ToFood()
    {
        var macros = HiddenMacrosDto;

        return new Food
        {
            Name = Name,
            Brands = Brands,
            Calories = Calories,
            Quantity = Quantity,
            Carbohydrates = macros.Carbohydrates,
            Fat = macros.Fat,
            Proteins = macros.Proteins
        };
    }
}

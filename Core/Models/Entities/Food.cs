using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Core.DTO.Foods;

namespace Core.Models.Entities;

public class Food
{
    [Key] public int Id { get; init; }

    public int MealId { get; init; }

    [JsonIgnore]
    [ForeignKey(nameof(MealId))]
    public Meal? Meal { get; set; }

    public double Quantity { get; set; }
    public int? Calories { get; set; }
    public string Name { get; set; } = "";
    public string Brands { get; set; } = "";
    public double Carbohydrates { get; set; }
    public double Fat { get; set; }
    public double Proteins { get; set; }

    public FoodDto ToDto()
    {
        return new FoodDto
        {
            HiddenMacrosDto = MacrosDto.From(Carbohydrates, Fat, Proteins, Calories),
            HiddenName = Name,
            Brands = Brands,
            Quantity = Quantity
        };
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Core.DTO.Foods;

namespace Core.Models.Entities;

public class Food
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();

    public int MealId { get; init; }

    [JsonIgnore]
    [ForeignKey(nameof(MealId))]
    public Meal? Meal { get; set; }

    public double Quantity { get; set; }
    public int? Calories { get; set; }
    public string? Name { get; set; }
    public string? Brands { get; set; }

    public ICollection<Macros> Macros { get; set; } = [];

    public FoodDto ToDto()
    {
        return new FoodDto
        {
            HiddenMacrosDto = MacrosDto.From(Macros),
            HiddenName = Name ?? "",
            Brands = Brands ?? "",
            Quantity = Quantity
        };
    }
}
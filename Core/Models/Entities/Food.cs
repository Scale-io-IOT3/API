using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Models.Entities;

public class Food
{
    [Key] public required string Id { get; init; }
    public int MealId { get; init; }
    [ForeignKey(nameof(MealId))] public Meal? Meal { get; set; }

    public double Quantity { get; set; }
    public int? Calories { get; set; }
    public required string Name { get; set; }
    public string? Brands { get; set; }

    public ICollection<Macros> Macros { get; set; } = [];
}
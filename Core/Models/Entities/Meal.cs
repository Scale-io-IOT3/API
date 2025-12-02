using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.DTO;

namespace Core.Models.Entities;

public class Meal
{
    [Key] public int Id { get; init; }
    public int UserId { get; init; }

    [ForeignKey(nameof(UserId))] public required User User { get; init; }

    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("yyyy-MM-dd");

    public ICollection<Food> Foods { get; set; } = [];

    public MealDto ToDto()
    {
        return new MealDto(this);
    }
}
using Core.DTO.Foods;
using Core.Models.Entities;

namespace Core.DTO;

public record MealDto
{
    public MealDto(Meal meal)
    {
        CreatedAt = meal.CreatedAt.ToUniversalTime().ToString("o");
        Foods = meal.Foods.Select(f => f.ToDto()).ToList();
    }

    public string CreatedAt { get; init; }
    public ICollection<FoodDto> Foods { get; init; }
}
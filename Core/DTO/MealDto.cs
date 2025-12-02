using Core.Models.Entities;

namespace Core.DTO;

public record MealDto
{
    public MealDto(Meal meal)
    {
        CreatedAt = meal.CreatedAt;
        Foods = meal.Foods;
    }

    public string CreatedAt { get; init; }
    public ICollection<Food> Foods { get; init; }
}
using System.ComponentModel.DataAnnotations;

namespace Core.Models.API.Requests;

public class MealCreationRequest : IValidatableObject
{
    [Required, MinLength(1), MaxLength(100)] public required MealFoodRequest[] Foods { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Foods is not null && Foods.Any(food => food is null))
            yield return new ValidationResult("Foods cannot contain null entries.", [nameof(Foods)]);
    }
}

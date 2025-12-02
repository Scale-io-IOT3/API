using System.ComponentModel.DataAnnotations;
using Core.DTO;

namespace Core.Models.API.Responses;

public class MealCreationResponse : Response
{
    [Required] public required MealDto Meal { get; init; }
}
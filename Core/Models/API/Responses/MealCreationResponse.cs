using System.ComponentModel.DataAnnotations;
using Core.Models.Entities;

namespace Core.Models.API.Responses;

public class MealCreationResponse : Response
{
    [Required] public required Meal Meal { get; init; }
}
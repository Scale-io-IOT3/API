using System.ComponentModel.DataAnnotations;
using Core.DTO.Foods;

namespace Core.Models.API.Requests;

public class MealCreationRequest
{
    [Required] public required FoodDto[] Foods { get; init; }
}
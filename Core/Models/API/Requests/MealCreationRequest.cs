using System.ComponentModel.DataAnnotations;
using Core.Models.Entities;

namespace Core.Models.API.Requests;

public class MealCreationRequest
{
    [Required] public required Food[] Foods { get; init; }
}
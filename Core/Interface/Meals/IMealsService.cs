using Core.Models.API.Requests;
using Core.Models.API.Responses;

namespace Core.Interface.Meals;

public interface IMealsService
{
    Task<MealCreationResponse> RegisterAsync(MealCreationRequest request, string username);
}
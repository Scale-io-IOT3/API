using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Core.Models.Entities;

namespace Core.Interface.Meals;

public interface IMealsService
{
    Task<MealCreationResponse?> RegisterAsync(MealCreationRequest request, string username);
    Task<List<Meal>> GetMeals(string username);
}
using Core.Interface;
using Core.Interface.Meals;
using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Core.Models.Entities;

namespace Infrastructure.Services.Meals;

public class MealServie(IRepo<User> users, IRepo<Meal> meals) : IMealsService
{
    public async Task<MealCreationResponse> RegisterAsync(MealCreationRequest request, string username)
    {
        var user = await users.FindByUsername(username);
        var meal = new Meal
        {
            Foods = request.Foods,
            User = user!
        };

        await meals.CreateOrUpdate(meal);
        return new MealCreationResponse
        {
            Meal = meal
        };
    }
}
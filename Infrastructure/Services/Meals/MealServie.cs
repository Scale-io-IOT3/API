using Core.Interface;
using Core.Interface.Meals;
using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Core.Models.Entities;

namespace Infrastructure.Services.Meals;

public class MealServie(IRepo<User> users, IRepo<Meal> meals) : IMealsService
{
    public async Task<MealCreationResponse?> RegisterAsync(MealCreationRequest request, string username)
    {
        var user = await GetUser(username);
        var meal = new Meal { Foods = request.Foods, User = user };

        await meals.CreateOrUpdate(meal);
        return new MealCreationResponse { Meal = meal };
    }

    public async Task<List<Meal>> GetMeals(string username)
    {
        var user = await GetUser(username);
        var m = await meals.GetAll();

        return m.FindAll(meal => meal.UserId == user.Id);
    }

    private async Task<User> GetUser(string username)
    {
        return (await users.FindByUsername(username))!;
    }
}
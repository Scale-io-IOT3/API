using Core.DTO;
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
        var meal = new Meal
        {
            User = user,
            Foods = request.Foods.Select(f => f.ToFood()).ToList()
        };

        await meals.CreateOrUpdate(meal);
        return new MealCreationResponse { Meal = meal.ToDto() };
    }

    public async Task<List<MealDto>> GetMeals(string username)
    {
        var user = await GetUser(username);
        var list = await meals.GetAll();

        return list.FindAll(meal => meal.UserId == user.Id).Select(m => m.ToDto()).ToList();
    }

    private async Task<User> GetUser(string username)
    {
        return (await users.FindByUsername(username))!;
    }
}
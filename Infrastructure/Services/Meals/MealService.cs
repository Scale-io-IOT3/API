using Core.DTO;
using Core.Interface;
using Core.Interface.Meals;
using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Core.Models.Entities;
using Infrastructure.Repositories;

namespace Infrastructure.Services.Meals;

/// <summary>Maps meal requests and responses while repositories manage persistence.</summary>
public sealed class MealService(IRepo<User> users, MealRepository meals) : IMealsService
{
    /// <inheritdoc />
    public async Task<MealCreationResponse?> RegisterAsync(MealCreationRequest request, string username)
    {
        var user = await GetUser(username);
        if (user is null) return null;

        var meal = new Meal
        {
            User = user,
            Foods = [.. request.Foods.Select(f => f.ToFood())]
        };

        await meals.CreateOrUpdate(meal);
        return new MealCreationResponse { Meal = meal.ToDto() };
    }

    /// <inheritdoc />
    public async Task<List<MealDto>> GetMeals(string username)
    {
        var user = await GetUser(username);
        if (user is null) return [];

        var list = await meals.GetByUserId(user.Id);
        return [.. list.Select(m => m.ToDto())];
    }

    private async Task<User?> GetUser(string username)
    {
        return await users.FindByUsername(username);
    }
}

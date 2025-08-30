using Core.DTO.FreshFoods;
using Core.Interface;
using Infrastructure.Clients;

namespace Infrastructure.Services;

public class FreshFoodsService(FoodsClient client) : IFoodService
{
    public async Task<FoodResponse?> FetchFood(string food)
    {
        var response = await client.FetchFood(food);
        return response?.Filter();
    }
}
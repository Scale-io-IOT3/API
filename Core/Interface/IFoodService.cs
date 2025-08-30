using Core.DTO.FreshFoods;

namespace Core.Interface;

public interface IFoodService
{
    public Task<FoodResponse?> FetchFood(string food);
}
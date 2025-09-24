using Core.DTO.Foods;
using Core.Interface.Foods;

namespace Infrastructure.Services.Foods.Abstract;

public abstract class FoodsService : IFoodService
{
    public virtual Task<FoodResponse?> FetchAsync(string input, double? grams) => FetchFromSource(input, grams);
    protected abstract Task<FoodResponse?> FetchFromSource(string input, double? grams);
}
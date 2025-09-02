using Core.DTO.Foods;
using Core.Interface;

namespace Infrastructure.Services.Abstract;

public abstract class Service : IService
{
    public virtual Task<FoodResponse?> FetchAsync(string input, double? grams) => FetchFromSource(input, grams);
    protected abstract Task<FoodResponse?> FetchFromSource(string input, double? grams);
}
using Core.DTO.Foods;

namespace Core.Interface;

public interface IService
{
    Task<FoodResponse?> FetchAsync(string input, double? grams = null);
}

public interface IBarcodeService : IService;

public interface IFreshFoodsService : IService;
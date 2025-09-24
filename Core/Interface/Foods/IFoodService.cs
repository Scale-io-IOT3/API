using Core.DTO.Foods;

namespace Core.Interface.Foods;

public interface IFoodService
{
    Task<FoodResponse?> FetchAsync(string input, double? grams = null);
}

public interface IBarcodeService : IFoodService;

public interface IFreshFoodsService : IFoodService;
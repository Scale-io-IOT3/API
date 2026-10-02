using Core.DTO.Foods;

namespace Core.Interface.Foods.Supervisors;

public interface IBarcodeSupervisor
{
    Task<FoodResponse?> ResolveAsync(string barcode, double? grams = null);
}

public interface IFreshFoodsSupervisor
{
    Task<FoodResponse?> ResolveAsync(string normalizedQuery, double? grams = null);
}

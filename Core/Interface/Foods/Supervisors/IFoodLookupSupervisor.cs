using Core.DTO.Foods;

namespace Core.Interface.Foods.Supervisors;

/// <summary>Coordinates barcode lookup after service-boundary validation.</summary>
public interface IBarcodeSupervisor
{
    /// <summary>Resolves a barcode into a consensus food and enriches its metadata.</summary>
    /// <param name="barcode">A digits-only barcode whose length and check digit have already been validated.</param>
    /// <param name="grams">Serving mass in grams; null or nonpositive values default to 100 grams.</param>
    /// <returns>A response containing the resolved food, or an empty food collection when unresolved.</returns>
    Task<FoodResponse?> ResolveAsync(string barcode, double? grams = null);
}

/// <summary>Coordinates text-based food lookup after service-boundary normalization.</summary>
public interface IFreshFoodsSupervisor
{
    /// <summary>Resolves a normalized query into ranked consensus foods and enriches their metadata.</summary>
    /// <param name="normalizedQuery">A normalized food query of at least two characters.</param>
    /// <param name="grams">Serving mass in grams; null or nonpositive values default to 100 grams.</param>
    /// <returns>A response containing matching foods, or an empty food collection when no matches exist.</returns>
    Task<FoodResponse?> ResolveAsync(string normalizedQuery, double? grams = null);
}

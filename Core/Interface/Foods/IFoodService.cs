using Core.DTO.Foods;

namespace Core.Interface.Foods;

/// <summary>Looks up foods from untrusted user input.</summary>
public interface IFoodService
{
    /// <summary>Validates the input and resolves matching foods with serving-specific nutrition.</summary>
    /// <param name="input">A barcode or food search query, depending on the service.</param>
    /// <param name="grams">Serving mass in grams; null or nonpositive values default to 100 grams.</param>
    /// <returns>The lookup response. Consensus implementations return an empty food collection for rejected input or no matches.</returns>
    Task<FoodResponse?> FetchAsync(string input, double? grams = null);
}

/// <summary>Identifies the food lookup service that accepts barcodes.</summary>
public interface IBarcodeService : IFoodService;

/// <summary>Identifies the food lookup service that accepts text queries.</summary>
public interface IFreshFoodsService : IFoodService;

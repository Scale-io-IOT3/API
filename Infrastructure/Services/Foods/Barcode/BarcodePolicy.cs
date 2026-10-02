

namespace Infrastructure.Services.Foods.Barcode;

internal static class BarcodePolicy
{
    internal const string BarcodeSource = "OpenFoodFactsBarcode";

    internal const string UsdaSource = "USDA";

    internal const string OpenFoodSearchSource = "OpenFoodFactsSearch";

    internal const string GtinBarcodeSource = "GTINSearchBarcode";

    internal const string GtinSearchSource = "GTINSearch";

    internal const int MaxSourceCandidates = 25;

    internal const double BarcodeReliability = 0.98;

    internal const double UsdaReliability = 0.95;

    internal const double OpenFoodSearchReliability = 0.75;

    internal const double GtinBarcodeReliability = 0.7;

    internal const double GtinSearchReliability = 0.65;

    internal const double MinIdentitySimilarity = 0.52;
}

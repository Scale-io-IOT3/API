

namespace Infrastructure.Services.Foods.Search;

internal static class SearchPolicy
{
    internal const string Usda = "USDA";

    internal const string OpenFoodFacts = "OpenFoodFacts";

    internal const string GtinSearch = "GTINSearch";

    internal const int MaxSourceCandidates = 25;

    internal const int MaxResults = 10;

    internal const int MinPrimaryCandidatesForFastPath = 4;

    internal const double UsdaReliability = 0.95;

    internal const double OpenFoodReliability = 0.75;

    internal const double GtinSearchReliability = 0.65;
}

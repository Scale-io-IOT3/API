

namespace Infrastructure.Services.Foods.Search;

internal static class SearchModels
{
    internal sealed record Candidate(
        string Source,
        string Name,
        string Brand,
        string NormalizedName,
        string NormalizedBrand,
        double Calories,
        double Carbohydrates,
        double Fat,
        double Proteins,
        double Weight,
        double MatchQuality,
        string? Grade
    );

    internal sealed record SourceCandidates(List<Candidate> Candidates, long LatencyMs);

    internal sealed record FoodCacheEntry(List<ConsensusFood> Foods, int ActiveSources, DateTimeOffset RefreshedAtUtc);

    internal sealed record RefreshResult(
        FoodCacheEntry Entry,
        long UsdaLatencyMs,
        long OpenFoodLatencyMs,
        long GtinLatencyMs
    );

    internal sealed record ConsensusFood(
        string Name,
        string Brand,
        double Calories,
        double Carbohydrates,
        double Fat,
        double Proteins,
        double Confidence,
        string[] Sources,
        double Relevance,
        string? Grade
    );
}



namespace Infrastructure.Services.Foods.Barcode;

/// <summary>Defines internal barcode pipeline snapshots; nutrient values are per 100 grams before response scaling.</summary>
internal static class BarcodeModels
{
    internal sealed record ConsensusResult(ConsensusFood? Food, int ActiveSources);

    internal sealed record RawCandidate(
        string Source,
        string Name,
        string Brand,
        double Calories,
        double Carbohydrates,
        double Fat,
        double Proteins,
        double Reliability,
        double QueryQuality,
        bool HasNutrition,
        string? Grade
    );

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
        double IdentitySimilarity,
        bool HasNutrition,
        string? Grade
    );

    internal sealed record SourceCandidates(List<Candidate> Candidates, long LatencyMs);

    internal sealed record RawSourceCandidates(List<RawCandidate> RawCandidates, long LatencyMs);

    internal sealed record BarcodeAnchorResult(RawCandidate? Anchor, long LatencyMs);

    internal sealed record BarcodeCacheEntry(
        ConsensusFood Consensus,
        int ActiveSources,
        DateTimeOffset RefreshedAtUtc,
        string IdentityQuery
    );

    internal sealed record RefreshResult(
        BarcodeCacheEntry? Entry,
        long BarcodeLatencyMs,
        long GtinBarcodeLatencyMs,
        long UsdaLatencyMs,
        long OpenFoodLatencyMs,
        long GtinSearchLatencyMs,
        string IdentityQuery
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
        string? Grade
    );
}

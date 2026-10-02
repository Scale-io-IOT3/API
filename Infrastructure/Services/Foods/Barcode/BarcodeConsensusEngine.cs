using Core.DTO.Foods;
using static Infrastructure.Services.Foods.Barcode.BarcodeModels;
using static Infrastructure.Services.Foods.Barcode.BarcodePolicy;
using static Infrastructure.Services.Foods.Shared.NutrientConsensus;
using static Infrastructure.Services.Foods.Barcode.BarcodeCandidateMapper;
using static Infrastructure.Validators.Foods.BarcodeCandidateValidator;

namespace Infrastructure.Services.Foods.Barcode;

internal static class BarcodeConsensusEngine
{
    internal static ConsensusResult Reconcile(
        Candidate anchor, RawSourceCandidates gtinBarcode, SourceCandidates usda,
        SourceCandidates openFood, SourceCandidates gtinSearch)
    {
        var candidates = new List<Candidate> { anchor };
        candidates.AddRange(gtinBarcode.RawCandidates
            .Select(raw => FinalizeRaw(raw, anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity));
        candidates.AddRange(usda.Candidates);
        candidates.AddRange(openFood.Candidates);
        candidates.AddRange(gtinSearch.Candidates);

        var activeSources = new HashSet<string>(StringComparer.Ordinal) { anchor.Source };
        if (usda.Candidates.Count > 0) activeSources.Add(UsdaSource);
        if (openFood.Candidates.Count > 0) activeSources.Add(OpenFoodSearchSource);
        if (gtinBarcode.RawCandidates.Count > 0) activeSources.Add(GtinBarcodeSource);
        if (gtinSearch.Candidates.Count > 0) activeSources.Add(GtinSearchSource);

        var aligned = AlignCandidatesToAnchor(anchor, candidates);
        return new ConsensusResult(BuildConsensus(aligned, anchor, activeSources.Count), activeSources.Count);
    }

    private static List<Candidate> AlignCandidatesToAnchor(Candidate anchor, List<Candidate> candidates)
    {
        var aligned = candidates
            .Where(candidate =>
                candidate.Source.Equals(anchor.Source, StringComparison.Ordinal) ||
                candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return aligned.Count == 0 ? [anchor] : aligned;
    }

    private static ConsensusFood? BuildConsensus(List<Candidate> candidates, Candidate anchor, int activeSources)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var nutritionCandidates = candidates.Where(candidate => candidate.HasNutrition).ToList();
        if (nutritionCandidates.Count == 0)
        {
            return null;
        }

        var calories = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Calories, candidate.Weight))]);
        var carbs = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Carbohydrates, candidate.Weight))]);
        var fat = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Fat, candidate.Weight))]);
        var proteins = Aggregate([.. nutritionCandidates.Select(candidate => (candidate.Proteins, candidate.Weight))]);
        var consensusCalories = ResolveCalories(calories.Value, carbs.Value, fat.Value, proteins.Value);

        var sourceCount = candidates.Select(candidate => candidate.Source).Distinct(StringComparer.Ordinal).Count();
        var agreeRatio = activeSources == 0 ? 0 : (double)sourceCount / activeSources;
        var variancePenalty = (calories.NormalizedMad + carbs.NormalizedMad + fat.NormalizedMad + proteins.NormalizedMad) / 4.0;
        var identityAgreement = candidates.Average(candidate => candidate.IdentitySimilarity);
        var sampleBoost = Math.Min(1.0, nutritionCandidates.Count / 4.0);
        var grade = SelectConsensusGrade(candidates);

        var confidence = Clamp(agreeRatio * identityAgreement * (1 - variancePenalty) * (0.6 + 0.4 * sampleBoost), 0.05, 0.99);

        return new ConsensusFood(
            anchor.Name,
            anchor.Brand,
            consensusCalories,
            carbs.Value,
            fat.Value,
            proteins.Value,
            confidence,
            [.. candidates
                .Select(candidate => candidate.Source)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(source => source)],
            grade
        );
    }

    private static string? SelectConsensusGrade(List<Candidate> candidates)
    {
        var grades = candidates
            .Select(candidate => new
            {
                Grade = NutritionGrade.Normalize(candidate.Grade),
                candidate.Weight
            })
            .Where(item => item.Grade is not null)
            .Select(item => new
            {
                Grade = item.Grade!,
                item.Weight
            })
            .ToList();

        if (grades.Count == 0)
        {
            return null;
        }

        return grades
            .GroupBy(item => item.Grade, StringComparer.Ordinal)
            .OrderByDescending(grouped => grouped.Sum(item => item.Weight))
            .ThenByDescending(grouped => grouped.Count())
            .ThenBy(grouped => grouped.Key, StringComparer.Ordinal)
            .Select(grouped => grouped.Key)
            .FirstOrDefault();
    }
}

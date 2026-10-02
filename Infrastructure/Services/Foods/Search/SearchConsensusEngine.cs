using Core.DTO.Foods;
using static Infrastructure.Services.Foods.Search.SearchModels;
using static Infrastructure.Services.Foods.Search.SearchPolicy;
using static Infrastructure.Services.Foods.Shared.FoodText;
using static Infrastructure.Services.Foods.Shared.NutrientConsensus;

namespace Infrastructure.Services.Foods.Search;

/// <summary>Clusters related search candidates and ranks their reconciled nutrition and confidence without I/O.</summary>
internal static class SearchConsensusEngine
{
    internal static List<ConsensusFood> BuildConsensus(List<Candidate> candidates, int activeSources)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var groups = Cluster(candidates);
        var consensus = groups
            .Select(group => BuildCluster(group, activeSources))
            .Where(item => item is not null)
            .Cast<ConsensusFood>()
            .OrderByDescending(item => item.Relevance)
            .Take(MaxResults)
            .ToList();

        return consensus;
    }

    private static ConsensusFood? BuildCluster(List<Candidate> group, int activeSources)
    {
        if (group.Count == 0)
        {
            return null;
        }

        var best = group.OrderByDescending(item => item.MatchQuality).First();
        var calories = Aggregate(group.Select(item => (item.Calories, item.Weight)).ToList());
        var carbs = Aggregate(group.Select(item => (item.Carbohydrates, item.Weight)).ToList());
        var fat = Aggregate(group.Select(item => (item.Fat, item.Weight)).ToList());
        var proteins = Aggregate(group.Select(item => (item.Proteins, item.Weight)).ToList());
        var consensusCalories = ResolveCalories(calories.Value, carbs.Value, fat.Value, proteins.Value);

        var sourceCount = group.Select(item => item.Source).Distinct(StringComparer.Ordinal).Count();
        var agreeRatio = activeSources == 0 ? 0 : (double)sourceCount / activeSources;
        var variancePenalty = (calories.NormalizedMad + carbs.NormalizedMad + fat.NormalizedMad + proteins.NormalizedMad) / 4.0;
        var sampleBoost = Math.Min(1.0, group.Count / 4.0);
        var confidence = Clamp(agreeRatio * (1 - variancePenalty) * (0.6 + 0.4 * sampleBoost), 0.05, 0.99);
        var relevance = confidence * 0.6 + best.MatchQuality * 0.4;
        var grade = SelectConsensusGrade(group);

        return new ConsensusFood(
            best.Name,
            best.Brand,
            consensusCalories,
            carbs.Value,
            fat.Value,
            proteins.Value,
            confidence,
            group.Select(item => item.Source).Distinct(StringComparer.Ordinal).OrderBy(s => s).ToArray(),
            relevance,
            grade
        );
    }

    private static List<List<Candidate>> Cluster(List<Candidate> candidates)
    {
        var groups = new List<List<Candidate>>();
        foreach (var candidate in candidates.OrderByDescending(c => c.MatchQuality))
        {
            var bestScore = 0.0;
            List<Candidate>? bestGroup = null;

            foreach (var group in groups)
            {
                var anchor = group[0];
                var score = 0.85 * TokenSimilarity(candidate.NormalizedName, anchor.NormalizedName) +
                            0.15 * TokenSimilarity(candidate.NormalizedBrand, anchor.NormalizedBrand);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestGroup = group;
                }
            }

            if (bestGroup is not null && bestScore >= 0.62)
            {
                bestGroup.Add(candidate);
            }
            else
            {
                groups.Add([candidate]);
            }
        }

        return groups;
    }

    private static string? SelectConsensusGrade(List<Candidate> group)
    {
        var grades = group
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

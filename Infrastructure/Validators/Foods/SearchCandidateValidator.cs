using static Infrastructure.Services.Foods.Search.SearchModels;

namespace Infrastructure.Validators.Foods;

/// <summary>Applies name and nutrition plausibility rules to text-search candidates.</summary>
internal static class SearchCandidateValidator
{
    /// <summary>Requires a name, at least one positive nutrition value, and per-100-gram upper bounds.</summary>
    /// <param name="candidate">The mapped provider candidate; calories are kcal and macronutrients are grams.</param>
    /// <returns>True when the candidate satisfies the search eligibility heuristics.</returns>
    /// <remarks>This heuristic does not independently validate every value's lower bound or finiteness.</remarks>
    internal static bool IsValid(Candidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Name))
        {
            return false;
        }

        var hasNutrition = candidate.Calories > 0 ||
                           candidate.Carbohydrates > 0 ||
                           candidate.Fat > 0 ||
                           candidate.Proteins > 0;

        if (!hasNutrition)
        {
            return false;
        }

        return candidate.Calories <= 1200 &&
               candidate.Carbohydrates <= 120 &&
               candidate.Fat <= 120 &&
               candidate.Proteins <= 120;
    }
}

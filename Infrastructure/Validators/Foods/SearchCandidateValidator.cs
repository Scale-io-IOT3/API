using static Infrastructure.Services.Foods.Search.SearchModels;

namespace Infrastructure.Validators.Foods;

internal static class SearchCandidateValidator
{
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

using static Infrastructure.Services.Foods.Barcode.BarcodeModels;

namespace Infrastructure.Validators.Foods;

/// <summary>Applies plausibility heuristics to barcode candidates while permitting identity-only anchors.</summary>
internal static class BarcodeCandidateValidator
{
    /// <summary>Requires a name and, when nutrition exists, checks per-100-gram upper bounds.</summary>
    /// <param name="candidate">The mapped provider candidate; calories are kcal and macronutrients are grams.</param>
    /// <returns>True for a named identity-only candidate or nutrition within the upper bounds.</returns>
    /// <remarks>This heuristic is not a complete numerical validation or an identity-match check.</remarks>
    internal static bool IsPlausible(Candidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Name))
        {
            return false;
        }

        if (!candidate.HasNutrition)
        {
            return true;
        }

        return candidate.Calories <= 1200 &&
               candidate.Carbohydrates <= 120 &&
               candidate.Fat <= 120 &&
               candidate.Proteins <= 120;
    }
}

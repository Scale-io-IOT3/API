using static Infrastructure.Services.Foods.Barcode.BarcodeModels;

namespace Infrastructure.Validators.Foods;

internal static class BarcodeCandidateValidator
{
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

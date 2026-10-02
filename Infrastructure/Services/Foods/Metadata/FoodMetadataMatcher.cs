using Core.DTO.Foods;
using Core.DTO.OpenFoodFacts;
using static Infrastructure.Services.Foods.Shared.FoodText;

namespace Infrastructure.Services.Foods.Metadata;

/// <summary>Ranks OpenFoodFacts metadata matches by normalized food name and brand.</summary>
internal static class FoodMetadataMatcher
{
    private const double MinMetadataSimilarity = 0.6;

    /// <summary>Selects usable metadata ranked by name and brand similarity.</summary>
    /// <param name="target">The response food whose identity should be matched.</param>
    /// <param name="products">Provider products to rank.</param>
    /// <returns>The preferred metadata, or null when no product has usable metadata.</returns>
    /// <remarks>If no ranked result meets the preferred similarity threshold, the best available result is still used.</remarks>
    internal static OpenFoodMetadata? SelectBestMetadata(FoodDto target, IEnumerable<OpenFoodSearchALiciousHit> products)
    {
        var normalizedName = Normalize(target.Name);
        var normalizedBrand = Normalize(target.Brands);

        var ranked = products
            .Select(product =>
            {
                var metadata = new OpenFoodMetadata(
                    NutritionGrade.Normalize(
                        product.NutriScoreGrade,
                        product.NutritionGrades,
                        product.NutritionGradeFr,
                        product.NutritionGradesTags?.FirstOrDefault()
                    ),
                    CloneNutrientLevels(product.NutrientLevels)
                );

                return new
                {
                    Metadata = metadata,
                    Name = Normalize(product.ResolvedName),
                    Brand = Normalize(product.ResolvedBrands)
                };
            })
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Name) &&
                (item.Metadata.Grade is not null ||
                 (item.Metadata.NutrientLevels is not null && item.Metadata.NutrientLevels.Count > 0)))
            .Select(item => new
            {
                item.Metadata,
                Score = 0.85 * TokenSimilarity(normalizedName, item.Name) +
                        0.15 * TokenSimilarity(normalizedBrand, item.Brand)
            })
            .OrderByDescending(item => item.Score);

        var best = ranked.FirstOrDefault(item => item.Score >= MinMetadataSimilarity);
        if (best is not null)
        {
            return best.Metadata;
        }

        return ranked.FirstOrDefault()?.Metadata;
    }

    internal static Dictionary<string, string>? CloneNutrientLevels(Dictionary<string, string>? levels)
    {
        return levels is null || levels.Count == 0
            ? null
            : levels.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }
}

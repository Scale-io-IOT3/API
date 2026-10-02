using Core.DTO.Foods;
using Core.DTO.GtinSearch;
using Core.DTO.OpenFoodFacts;
using static Infrastructure.Services.Foods.Barcode.BarcodeModels;
using static Infrastructure.Services.Foods.Barcode.BarcodePolicy;
using static Infrastructure.Services.Foods.Shared.FoodText;
using static Infrastructure.Services.Foods.Shared.ExternalFoodFields;
using static Infrastructure.Services.Foods.Shared.NutrientConsensus;

namespace Infrastructure.Services.Foods.Barcode;

/// <summary>Converts provider payloads into barcode identity and nutrition candidates without performing I/O.</summary>
internal static class BarcodeCandidateMapper
{
    internal static RawCandidate? ToRawFromOpenFoodSearch(OpenFoodSearchProduct product)
    {
        if (string.IsNullOrWhiteSpace(product.Name) || product.Nutriments is null)
        {
            return null;
        }

        var carbs = product.Nutriments.Carbohydrates100g ?? 0;
        var fat = product.Nutriments.Fat100g ?? 0;
        var proteins = product.Nutriments.Proteins100g ?? 0;
        var calories = ResolveCalories(product.Nutriments.EnergyKcal100g ?? 0, carbs, fat, proteins);

        return new RawCandidate(
            OpenFoodSearchSource,
            product.Name,
            product.Brands ?? string.Empty,
            calories,
            carbs,
            fat,
            proteins,
            OpenFoodSearchReliability,
            0.8,
            HasNutrition(calories, carbs, fat, proteins),
            NutritionGrade.Normalize(
                product.NutriScoreGrade,
                product.NutritionGrades,
                product.NutritionGradeFr,
                product.NutritionGradesTags?.FirstOrDefault()
            )
        );
    }

    internal static RawCandidate? ToRawFromGtin(
        GtinSearchItem item,
        string source,
        double queryQuality,
        double reliability
    )
    {
        var name = FirstNonEmpty(item.Name, ExtractText(item.Extra, NameKeys));
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var brand = FirstNonEmpty(item.BrandName, item.Brand, ExtractText(item.Extra, BrandKeys));
        var carbs = ExtractNumber(item.Extra, CarbohydrateKeys) ?? 0;
        var fat = ExtractNumber(item.Extra, FatKeys) ?? 0;
        var proteins = ExtractNumber(item.Extra, ProteinKeys) ?? 0;
        var calories = ResolveCalories(ExtractNumber(item.Extra, CaloriesKeys) ?? 0, carbs, fat, proteins);

        return new RawCandidate(
            source,
            name,
            brand,
            calories,
            carbs,
            fat,
            proteins,
            reliability,
            queryQuality,
            HasNutrition(calories, carbs, fat, proteins),
            null
        );
    }

    internal static RawCandidate ToRawFromDto(string source, FoodDto dto, string query, double reliability)
    {
        var calories = ResolveCalories(dto.Calories, dto.MacrosDto.Carbohydrates, dto.MacrosDto.Fat, dto.MacrosDto.Proteins);
        return new RawCandidate(
            source,
            dto.Name,
            dto.Brands,
            calories,
            dto.MacrosDto.Carbohydrates,
            dto.MacrosDto.Fat,
            dto.MacrosDto.Proteins,
            reliability,
            MatchQuality(query, Normalize(dto.Name)),
            HasNutrition(dto.Calories, dto.MacrosDto.Carbohydrates, dto.MacrosDto.Fat, dto.MacrosDto.Proteins),
            dto.Grade
        );
    }

    internal static Candidate CreateAnchor(RawCandidate raw)
    {
        var normalizedName = Normalize(raw.Name);
        var normalizedBrand = Normalize(raw.Brand);
        var weight = Math.Max(0.1, raw.Reliability);

        return new Candidate(
            raw.Source,
            raw.Name,
            raw.Brand,
            normalizedName,
            normalizedBrand,
            raw.Calories,
            raw.Carbohydrates,
            raw.Fat,
            raw.Proteins,
            weight,
            raw.QueryQuality,
            1.0,
            raw.HasNutrition,
            raw.Grade
        );
    }

    internal static Candidate FinalizeRaw(RawCandidate raw, Candidate anchor)
    {
        var normalizedName = Normalize(raw.Name);
        var normalizedBrand = Normalize(raw.Brand);
        var identitySimilarity = IdentitySimilarity(anchor.NormalizedName, anchor.NormalizedBrand, normalizedName, normalizedBrand);
        var blendedQuality = 0.55 * raw.QueryQuality + 0.45 * identitySimilarity;
        var weight = Math.Max(0.1, raw.Reliability * blendedQuality);

        return new Candidate(
            raw.Source,
            raw.Name,
            raw.Brand,
            normalizedName,
            normalizedBrand,
            raw.Calories,
            raw.Carbohydrates,
            raw.Fat,
            raw.Proteins,
            weight,
            raw.QueryQuality,
            identitySimilarity,
            raw.HasNutrition,
            raw.Grade
        );
    }
}

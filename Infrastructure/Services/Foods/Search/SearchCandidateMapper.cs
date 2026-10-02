using Core.DTO.Foods;
using Core.DTO.GtinSearch;
using Core.DTO.OpenFoodFacts;
using static Infrastructure.Services.Foods.Search.SearchModels;
using static Infrastructure.Services.Foods.Search.SearchPolicy;
using static Infrastructure.Services.Foods.Shared.FoodText;
using static Infrastructure.Services.Foods.Shared.ExternalFoodFields;
using static Infrastructure.Services.Foods.Shared.NutrientConsensus;
using static Infrastructure.Services.Foods.Metadata.FoodMetadataMatcher;
using static Infrastructure.Validators.Foods.SearchCandidateValidator;

namespace Infrastructure.Services.Foods.Search;

internal static class SearchCandidateMapper
{
    internal static Candidate? FromOpenFood(OpenFoodSearchProduct product, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(product.Name) || product.Nutriments is null)
        {
            return null;
        }

        var carbs = product.Nutriments.Carbohydrates100g ?? 0;
        var fat = product.Nutriments.Fat100g ?? 0;
        var proteins = product.Nutriments.Proteins100g ?? 0;
        var calories = ResolveCalories(product.Nutriments.EnergyKcal100g ?? 0, carbs, fat, proteins);

        var dto = new FoodDto
        {
            HiddenName = product.Name,
            Brands = product.Brands ?? string.Empty,
            HiddenMacrosDto = MacrosDto.From(carbs, fat, proteins, (int)Math.Round(calories)),
            Grade = NutritionGrade.Normalize(
                product.NutriScoreGrade,
                product.NutritionGrades,
                product.NutritionGradeFr,
                product.NutritionGradesTags?.FirstOrDefault()
            ),
            NutrientLevels = CloneNutrientLevels(product.NutrientLevels),
        };

        var candidate = FromDto(OpenFoodFacts, dto, normalizedQuery, OpenFoodReliability);
        return IsValid(candidate) ? candidate : null;
    }

    internal static Candidate? FromGtinSearch(GtinSearchItem item, string normalizedQuery)
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

        var dto = new FoodDto
        {
            HiddenName = name,
            Brands = brand,
            HiddenMacrosDto = MacrosDto.From(carbs, fat, proteins, (int)Math.Round(calories))
        };

        var candidate = FromDto(GtinSearch, dto, normalizedQuery, GtinSearchReliability);
        return IsValid(candidate) ? candidate : null;
    }

    internal static Candidate FromDto(string source, FoodDto dto, string normalizedQuery, double reliability)
    {
        var normalizedName = Normalize(dto.Name);
        var normalizedBrand = Normalize(dto.Brands);
        var matchQuality = MatchQuality(normalizedQuery, normalizedName);
        var weight = Math.Max(0.1, reliability * matchQuality);
        var calories = ResolveCalories(dto.Calories, dto.MacrosDto.Carbohydrates, dto.MacrosDto.Fat, dto.MacrosDto.Proteins);

        return new Candidate(
            source,
            dto.Name,
            dto.Brands,
            normalizedName,
            normalizedBrand,
            calories,
            dto.MacrosDto.Carbohydrates,
            dto.MacrosDto.Fat,
            dto.MacrosDto.Proteins,
            weight,
            matchQuality,
            dto.Grade
        );
    }
}

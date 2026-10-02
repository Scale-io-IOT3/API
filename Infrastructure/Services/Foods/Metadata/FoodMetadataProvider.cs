using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Microsoft.Extensions.Logging;
using static Infrastructure.Services.Foods.Metadata.FoodMetadataMatcher;

namespace Infrastructure.Services.Foods.Metadata;

internal sealed class FoodMetadataProvider(
    IClient<BarcodeResponse> barcodeClient,
    IClient<OpenFoodSearchALiciousResponse> openFoodSearchALiciousClient,
    ILogger<FoodMetadataProvider> logger
)
{
    internal async Task<OpenFoodMetadata?> FetchBarcodeMetadataAsync(FoodDto food, string barcode)
    {
        OpenFoodMetadata? barcodeMetadata = null;
        try
        {
            var barcodeResponse = await barcodeClient.Fetch(barcode);
            var product = barcodeResponse?.Product;
            if (product is not null)
            {
                barcodeMetadata = new OpenFoodMetadata(
                    NutritionGrade.Normalize(product.ResolvedNutritionGrade),
                    product.ResolvedNutrientLevels
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "OpenFoodFacts barcode metadata lookup failed. barcode='{Barcode}', food='{Food}'",
                barcode,
                food.Name
            );
        }

        var hasGrade = NutritionGrade.Normalize(barcodeMetadata?.Grade) is not null;
        var hasNutrientLevels = barcodeMetadata?.NutrientLevels is not null && barcodeMetadata.NutrientLevels.Count > 0;
        if (hasGrade && hasNutrientLevels)
        {
            return barcodeMetadata;
        }

        var fallback = await FetchBestOpenFoodMetadataAsync(food);
        if (fallback is null)
        {
            return hasGrade || hasNutrientLevels ? barcodeMetadata : null;
        }

        var grade = hasGrade ? NutritionGrade.Normalize(barcodeMetadata!.Grade) : NutritionGrade.Normalize(fallback.Grade);
        var nutrientLevels = hasNutrientLevels
            ? CloneNutrientLevels(barcodeMetadata!.NutrientLevels)
            : CloneNutrientLevels(fallback.NutrientLevels);

        if (grade is null && (nutrientLevels is null || nutrientLevels.Count == 0))
        {
            return null;
        }

        return new OpenFoodMetadata(grade, nutrientLevels);
    }

    internal async Task<OpenFoodMetadata?> FetchBestOpenFoodMetadataAsync(FoodDto food)
    {
        var queries = new[]
        {
            $"{food.Brands} {food.Name}".Trim(),
            food.Name
        }
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
        {
            try
            {
                var response = await openFoodSearchALiciousClient.Fetch(query);
                var metadata = SelectBestMetadata(food, response?.Hits ?? []);
                if (metadata is not null)
                {
                    return metadata;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "OpenFoodFacts metadata lookup failed. food='{Food}', query='{Query}'",
                    food.Name,
                    query
                );
            }
        }

        return null;
    }
}

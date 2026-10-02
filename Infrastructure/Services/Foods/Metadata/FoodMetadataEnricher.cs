using Core.DTO.Foods;
using Microsoft.Extensions.Configuration;
using static Infrastructure.Services.Foods.Shared.FoodText;
using static Infrastructure.Services.Foods.Metadata.FoodMetadataMatcher;

namespace Infrastructure.Services.Foods.Metadata;

/// <summary>Fills missing response metadata without replacing existing values or sharing mutable dictionaries.</summary>
internal sealed class FoodMetadataEnricher(
    FoodMetadataCache metadataCache,
    FoodMetadataProvider provider,
    IConfiguration configuration
)
{
    private readonly int _metadataMaxParallelism = Math.Clamp(
        SourceSettingsResolver.ReadGlobalInt(configuration, "MetadataMaxParallelism", 4),
        1,
        8
    );

    /// <summary>Fills missing grade and nutrient levels on the supplied response DTO.</summary>
    /// <param name="food">The request-specific DTO to mutate; existing metadata is preserved.</param>
    /// <param name="barcode">The validated barcode used for metadata lookup and caching.</param>
    /// <returns>A task completing when enrichment finishes.</returns>
    internal async Task EnrichBarcodeAsync(FoodDto food, string barcode)
    {
        var needsGrade = NutritionGrade.Normalize(food.Grade) is null;
        var needsNutrientLevels = food.NutrientLevels is null || food.NutrientLevels.Count == 0;

        if (!needsGrade && !needsNutrientLevels)
        {
            return;
        }

        var metadata = await metadataCache.GetAsync($"openfood_metadata_barcode_{barcode}", () => provider.FetchBarcodeMetadataAsync(food, barcode));
        if (metadata is null)
        {
            return;
        }

        var resolvedGrade = NutritionGrade.Normalize(metadata.Grade);
        if (needsGrade && resolvedGrade is not null)
        {
            food.Grade = resolvedGrade;
        }

        if (needsNutrientLevels && metadata.NutrientLevels is not null && metadata.NutrientLevels.Count > 0)
        {
            food.NutrientLevels = CloneNutrientLevels(metadata.NutrientLevels);
        }
    }

    /// <summary>Enriches missing metadata, grouping duplicate identities and bounding lookup concurrency.</summary>
    /// <param name="foods">Request-specific DTOs to mutate; each receives its own metadata dictionary.</param>
    /// <returns>A task completing when all enrichment work finishes.</returns>
    /// <remarks>The configured parallelism limit applies to this invocation, not across the entire process.</remarks>
    internal async Task EnrichSearchAsync(FoodDto[] foods)
    {
        var missing = foods
            .Where(NeedsMetadata)
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        using var semaphore = new SemaphoreSlim(_metadataMaxParallelism, _metadataMaxParallelism);
        var tasks = missing
            .GroupBy(BuildMetadataIdentityKey, StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(async group =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var metadata = await metadataCache.GetAsync($"openfood_metadata_search_{group.Key}", () => provider.FetchBestOpenFoodMetadataAsync(group.First()));
                    if (metadata is null)
                    {
                        return;
                    }

                    foreach (var food in group)
                    {
                        ApplyMetadata(food, metadata);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

        await Task.WhenAll(tasks);
    }

    private static string BuildMetadataIdentityKey(FoodDto food)
    {
        var normalizedName = Normalize(food.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return string.Empty;
        }

        var normalizedBrand = Normalize(food.Brands);
        return $"{normalizedBrand}|{normalizedName}";
    }

    private static bool NeedsMetadata(FoodDto food)
    {
        return NutritionGrade.Normalize(food.Grade) is null ||
               food.NutrientLevels is null ||
               food.NutrientLevels.Count == 0;
    }

    private static void ApplyMetadata(FoodDto food, OpenFoodMetadata metadata)
    {
        var normalizedCurrentGrade = NutritionGrade.Normalize(food.Grade);
        var normalizedResolvedGrade = NutritionGrade.Normalize(metadata.Grade);
        if (normalizedCurrentGrade is null && normalizedResolvedGrade is not null)
        {
            food.Grade = normalizedResolvedGrade;
        }

        if ((food.NutrientLevels is null || food.NutrientLevels.Count == 0) &&
            metadata.NutrientLevels is not null &&
            metadata.NutrientLevels.Count > 0)
        {
            food.NutrientLevels = CloneNutrientLevels(metadata.NutrientLevels);
        }
    }
}

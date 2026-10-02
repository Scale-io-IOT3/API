namespace Infrastructure.Services.Foods.Metadata;

/// <summary>Carries optional OpenFoodFacts grade and nutrient-level metadata independently of nutrition consensus.</summary>
internal sealed record OpenFoodMetadata(
    string? Grade,
    Dictionary<string, string>? NutrientLevels
);

/// <summary>Wraps metadata so a cached negative result can be distinguished from a cache miss.</summary>
internal sealed record OpenFoodMetadataCacheEntry(OpenFoodMetadata? Metadata);

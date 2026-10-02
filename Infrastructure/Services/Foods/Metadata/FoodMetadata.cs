namespace Infrastructure.Services.Foods.Metadata;

internal sealed record OpenFoodMetadata(
    string? Grade,
    Dictionary<string, string>? NutrientLevels
);

internal sealed record OpenFoodMetadataCacheEntry(OpenFoodMetadata? Metadata);

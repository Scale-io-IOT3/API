using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Core.Models.Entities;

namespace Core.Models.API.Requests;

public sealed class MealFoodRequest : IValidatableObject
{
    [StringLength(255)] public string? Name { get; init; }
    [StringLength(255)] public string Brands { get; init; } = "";
    public double Quantity { get; init; }
    [Range(0, int.MaxValue)] public int? Calories { get; init; }
    public MealMacrosRequest? Macros { get; init; }

    [StringLength(255), JsonPropertyName("product_name")] public string? LegacyName { get; init; }
    [JsonPropertyName("nutriments")] public MealMacrosRequest? LegacyMacros { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name ?? LegacyName))
            yield return new ValidationResult("A food name is required.", [nameof(Name)]);
        if (!double.IsFinite(Quantity) || Quantity <= 0)
            yield return new ValidationResult("Quantity must be finite and greater than zero.", [nameof(Quantity)]);
        if (Macros is null && LegacyMacros is null)
            yield return new ValidationResult("Macros are required.", [nameof(Macros)]);
        if (Brands is null)
            yield return new ValidationResult("Brands cannot be null.", [nameof(Brands)]);
        var macros = Macros ?? LegacyMacros;
        if (Calories is null && macros is not null && macros.Calories is null && macros.LegacyCalories is null &&
            macros.Carbohydrates * 4 + macros.Fat * 9 + macros.Proteins * 4 > int.MaxValue)
            yield return new ValidationResult("Computed calories exceed the supported range.", [nameof(Calories)]);
    }

    public Food ToFood()
    {
        var macros = Macros ?? LegacyMacros!;
        return new Food
        {
            Name = (Name ?? LegacyName)!,
            Brands = Brands,
            Quantity = Quantity,
            Calories = Calories ?? macros.Calories ?? (int)Math.Round(macros.LegacyCalories ??
                macros.Carbohydrates * 4 + macros.Fat * 9 + macros.Proteins * 4),
            Carbohydrates = macros.Carbohydrates,
            Fat = macros.Fat,
            Proteins = macros.Proteins
        };
    }
}

public sealed class MealMacrosRequest : IValidatableObject
{
    public double Carbohydrates { get; init; }
    public double Fat { get; init; }
    public double Proteins { get; init; }
    [Range(0, int.MaxValue)] public int? Calories { get; init; }
    [JsonPropertyName("energy-kcal_value_computed")] public double? LegacyCalories { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, value) in new[]
                 {
                     (nameof(Carbohydrates), Carbohydrates), (nameof(Fat), Fat),
                     (nameof(Proteins), Proteins)
                 })
            if (!double.IsFinite(value) || value < 0)
                yield return new ValidationResult("Macro values must be finite and nonnegative.", [name]);
        if (LegacyCalories is { } calories &&
            (!double.IsFinite(calories) || calories < 0 || calories > int.MaxValue))
            yield return new ValidationResult("Calories must be finite and nonnegative.", [nameof(LegacyCalories)]);
    }
}

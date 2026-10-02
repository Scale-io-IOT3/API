using Infrastructure.Services.Foods.Shared;

namespace Infrastructure.Validators.Foods;

/// <summary>Applies text normalization and minimum query length without calling providers.</summary>
internal static class SearchInputValidator
{
    /// <summary>Normalizes case, accents, punctuation, and whitespace before checking query length.</summary>
    /// <param name="input">The raw search text.</param>
    /// <param name="query">The normalized text, including when validation fails.</param>
    /// <returns>True when the normalized query contains at least two characters.</returns>
    internal static bool TryNormalize(string input, out string query)
    {
        query = FoodText.Normalize(input);
        return query.Length >= 2;
    }
}

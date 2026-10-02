using System.Globalization;
using System.Text;

namespace Infrastructure.Services.Foods.Shared;

internal static class FoodText
{
    internal static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    internal static double MatchQuality(string query, string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(normalizedName))
        {
            return 0.1;
        }

        if (normalizedName.Equals(query, StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalizedName.StartsWith(query, StringComparison.Ordinal))
        {
            return 0.9;
        }

        if (normalizedName.Contains(query, StringComparison.Ordinal))
        {
            return 0.75;
        }

        var similarity = TokenSimilarity(query, normalizedName);
        return Math.Max(0.2, 0.2 + similarity * 0.6);
    }

    internal static double TokenSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        if (left.Equals(right, StringComparison.Ordinal))
        {
            return 1;
        }

        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    internal static double IdentitySimilarity(
        string anchorNormalizedName,
        string anchorNormalizedBrand,
        string candidateNormalizedName,
        string candidateNormalizedBrand
    )
    {
        var nameSimilarity = TokenSimilarity(anchorNormalizedName, candidateNormalizedName);
        var brandSimilarity = TokenSimilarity(anchorNormalizedBrand, candidateNormalizedBrand);

        if (string.IsNullOrWhiteSpace(anchorNormalizedBrand))
        {
            return nameSimilarity;
        }

        return 0.8 * nameSimilarity + 0.2 * brandSimilarity;
    }

    internal static string BuildIdentityQuery(string name, string brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return Normalize(name);
        }

        return Normalize($"{brand} {name}");
    }
}

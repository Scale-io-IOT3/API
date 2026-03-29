using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Core.Interface;

namespace Core.DTO.FreshFoods;

public class FreshFoodResponse : IResponse
{
    [JsonPropertyName("foods")] public required FreshFood[] Foods { get; init; }

    public FreshFoodResponse Filter()
    {
        return FilterAndRank(string.Empty, int.MaxValue);
    }

    public FreshFoodResponse FilterAndRank(string query, int maxResults)
    {
        var normalizedQuery = NormalizeQuery(query);
        var queryTerms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var filtered = Foods
            .Select(f => new FreshFood
            {
                Description = f.Description,
                Category = f.Category,
                FoodNutrients = f.GetMacros()
            })
            .Select(food => new { Food = food, Score = Score(food.Description, normalizedQuery, queryTerms) })
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery) || item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Food.Description, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(item => item.Food)
            .ToArray();

        return new FreshFoodResponse { Foods = filtered };
    }

    private static int Score(string description, string normalizedQuery, string[] queryTerms)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery)) return 1;
        
        var normalizedDescription = NormalizeQuery(description);
        if (normalizedDescription.Length == 0) return 0;

        if (normalizedDescription.Equals(normalizedQuery, StringComparison.Ordinal)) return 1000;

        if (normalizedDescription.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 700;

        if (normalizedDescription.Contains(normalizedQuery, StringComparison.Ordinal)) return 400;

        var descriptionTerms = normalizedDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var score = 0;

        foreach (var term in queryTerms)
        {
            if (descriptionTerms.Contains(term, StringComparer.Ordinal))
            {
                score += 120;
            }
            else if (normalizedDescription.Contains(term, StringComparison.Ordinal))
            {
                score += 40;
            }
        }

        return score;
    }

    private static string NormalizeQuery(string value)
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
}

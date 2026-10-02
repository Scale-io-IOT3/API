using System.Globalization;

namespace Infrastructure.Services.Foods.Shared;

internal static class ExternalFoodFields
{
    internal static readonly HashSet<string> NameKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "product_name",
        "title",
        "description"
    };

    internal static readonly HashSet<string> BrandKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "brand",
        "brand_name",
        "manufacturer"
    };

    internal static readonly HashSet<string> CaloriesKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "calories",
        "kcal",
        "energy",
        "energy_kcal",
        "energy-kcal",
        "energy-kcal_100g"
    };

    internal static readonly HashSet<string> CarbohydrateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "carbohydrates",
        "carbs",
        "carbohydrate",
        "carbohydrates_100g"
    };

    internal static readonly HashSet<string> FatKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "fat",
        "total_fat",
        "fat_100g"
    };

    internal static readonly HashSet<string> ProteinKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "protein",
        "proteins",
        "protein_100g",
        "proteins_100g"
    };

    internal static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    internal static string? ExtractText(Dictionary<string, System.Text.Json.JsonElement> source, HashSet<string> keys)
    {
        foreach (var entry in source)
        {
            if (keys.Contains(entry.Key) && entry.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var value = entry.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            var nested = ExtractText(entry.Value, keys);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        return null;
    }

    internal static string? ExtractText(System.Text.Json.JsonElement element, HashSet<string> keys)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .SelectMany(prop =>
                {
                    var direct = keys.Contains(prop.Name) && prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                        ? new[] { prop.Value.GetString() }
                        : [];
                    var nested = ExtractText(prop.Value, keys);
                    return direct.Append(nested);
                })
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray()
                .Select(item => ExtractText(item, keys))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };
    }

    internal static double? ExtractNumber(Dictionary<string, System.Text.Json.JsonElement> source, HashSet<string> keys)
    {
        foreach (var entry in source)
        {
            if (keys.Contains(entry.Key))
            {
                var parsed = ParseNumber(entry.Value);
                if (parsed is not null)
                {
                    return parsed;
                }
            }

            var nested = ExtractNumber(entry.Value, keys);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    internal static double? ExtractNumber(System.Text.Json.JsonElement element, HashSet<string> keys)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .SelectMany(prop =>
                {
                    var direct = keys.Contains(prop.Name)
                        ? new[] { ParseNumber(prop.Value) }
                        : [];
                    var nested = ExtractNumber(prop.Value, keys);
                    return direct.Append(nested);
                })
                .FirstOrDefault(value => value is not null),
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray()
                .Select(item => ExtractNumber(item, keys))
                .FirstOrDefault(value => value is not null),
            _ => null
        };
    }

    internal static double? ParseNumber(System.Text.Json.JsonElement value)
    {
        if (value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}

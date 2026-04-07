namespace Core.DTO.Foods;

public static class NutritionGrade
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var token = raw.Trim().ToLowerInvariant();
        var colonIndex = token.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex < token.Length - 1)
        {
            token = token[(colonIndex + 1)..];
        }

        const string nutriPrefix = "nutriscore-grade-";
        if (token.StartsWith(nutriPrefix, StringComparison.Ordinal))
        {
            token = token[nutriPrefix.Length..];
        }

        if (token.Length != 1) return null;

        var grade = token[0];
        return grade is >= 'a' and <= 'e'
            ? char.ToUpperInvariant(grade).ToString()
            : null;
    }

    public static string? Normalize(params string?[] raws)
    {
        foreach (var raw in raws)
        {
            var normalized = Normalize(raw);
            if (normalized is not null)
            {
                return normalized;
            }
        }

        return null;
    }

    public static string? GetLabel(string? grade)
    {
        return Normalize(grade) switch
        {
            "A" => "good",
            "B" => "good",
            "C" => "moderate",
            "D" => "limit",
            "E" => "avoid",
            _ => null
        };
    }
}

using Infrastructure.Services.Foods.Shared;

namespace Infrastructure.Validators.Foods;

internal static class SearchInputValidator
{
    internal static bool TryNormalize(string input, out string query)
    {
        query = FoodText.Normalize(input);
        return query.Length >= 2;
    }
}

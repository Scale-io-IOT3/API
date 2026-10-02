using Core.DTO;
using Core.Models.API.Requests;
using Core.Models.API.Responses;

namespace Core.Interface.Meals;

/// <summary>Persists and retrieves meals owned by an authenticated user.</summary>
public interface IMealsService
{
    /// <summary>Creates a meal from the supplied food snapshots.</summary>
    /// <param name="request">The validated meal input.</param>
    /// <param name="username">The authenticated owner's username, not a client-selected identity.</param>
    /// <returns>The persisted meal, or null when the user does not exist.</returns>
    Task<MealCreationResponse?> RegisterAsync(MealCreationRequest request, string username);
    /// <summary>Retrieves meals belonging to the specified user.</summary>
    /// <param name="username">The authenticated owner's username.</param>
    /// <returns>The user's meals; an empty list when the user is missing or has no meals.</returns>
    Task<List<MealDto>> GetMeals(string username);
}

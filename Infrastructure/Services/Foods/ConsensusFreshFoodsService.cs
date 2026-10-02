using Core.DTO.Foods;
using Core.Interface.Foods;
using Core.Interface.Foods.Supervisors;
using Infrastructure.Validators.Foods;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Foods;

public sealed class ConsensusFreshFoodsService(IFreshFoodsSupervisor supervisor, ILogger<ConsensusFreshFoodsService> logger) : IFreshFoodsService
{
    public Task<FoodResponse?> FetchAsync(string input, double? grams = null)
    {
        if (SearchInputValidator.TryNormalize(input, out var query))
            return supervisor.ResolveAsync(query, grams);

        logger.LogInformation("Food search request rejected. input='{Input}'", input);
        return Task.FromResult<FoodResponse?>(new() { Foods = [] });
    }
}

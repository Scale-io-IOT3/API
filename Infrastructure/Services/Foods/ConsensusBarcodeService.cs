using Core.DTO.Foods;
using Core.Interface.Foods;
using Core.Interface.Foods.Supervisors;
using Infrastructure.Validators.Foods;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Foods;

/// <summary>Validates barcode requests before delegating provider orchestration to the supervisor.</summary>
public sealed class ConsensusBarcodeService(IBarcodeSupervisor supervisor, ILogger<ConsensusBarcodeService> logger) : IBarcodeService
{
    /// <inheritdoc />
    public Task<FoodResponse?> FetchAsync(string input, double? grams = null)
    {
        if (BarcodeInputValidator.TryNormalize(input, out var barcode))
            return supervisor.ResolveAsync(barcode, grams);

        logger.LogInformation("Barcode request rejected. input='{Input}'", input);
        return Task.FromResult<FoodResponse?>(new() { Foods = [] });
    }
}

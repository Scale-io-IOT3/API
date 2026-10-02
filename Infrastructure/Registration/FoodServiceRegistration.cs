using Core.Interface.Foods;
using Core.Interface.Foods.Supervisors;
using Infrastructure.Services.Foods;
using Infrastructure.Services.Foods.Barcode;
using Infrastructure.Services.Foods.Metadata;
using Infrastructure.Services.Foods.Search;
using Infrastructure.Supervisors.Foods;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static partial class DependencyInjection
{
    private static void AddFoodServices(this IServiceCollection services)
    {
        services.AddSingleton<BarcodeSourceProvider>();
        services.AddSingleton<SearchSourceProvider>();
        services.AddSingleton<FoodMetadataProvider>();
        services.AddSingleton<FoodMetadataCache>();
        services.AddSingleton<FoodMetadataEnricher>();
        services.AddSingleton<IBarcodeSupervisor, BarcodeSupervisor>();
        services.AddSingleton<IFreshFoodsSupervisor, FreshFoodsSupervisor>();
        services.AddSingleton<IBarcodeService, ConsensusBarcodeService>();
        services.AddSingleton<IFreshFoodsService, ConsensusFreshFoodsService>();
    }
}

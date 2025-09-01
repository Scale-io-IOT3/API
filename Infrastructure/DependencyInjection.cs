using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.Interface;
using Infrastructure.Clients;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped();
        services.AddClients();
    }

    private static void AddClients(this IServiceCollection services)
    {
        services.AddHttpClient<IApiClient<BarcodeResponse>, BarcodeClient>();
        services.AddHttpClient<IApiClient<FreshFoodResponse>, FoodsClient>();
    }

    private static void AddScoped(this IServiceCollection services)
    {
        services.AddScoped<IBarcodeService, BarcodeFoodService>();
        services.AddScoped<IFreshFoodsService, FreshFoodService>();
    }
}
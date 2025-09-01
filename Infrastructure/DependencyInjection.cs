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
        services.AddHttpClient<BarcodeClient>();
        services.AddHttpClient<FoodsClient>();
    }

    private static void AddScoped(this IServiceCollection services)
    {
        services.AddScoped<ServiceFactory>();
        services.AddScoped<IBarcodeService, BarcodeService>();
        services.AddScoped<IFreshFoodsService, FreshFoodsService>();
    }
}
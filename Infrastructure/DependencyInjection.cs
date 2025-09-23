using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.Interface;
using Infrastructure.Clients;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Services;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(configuration["DB_CONNECTION_STRING"]);
        });
        services.AddMemoryCache();
        services.AddScoped();
        services.AddClients();
    }

    private static void AddClients(this IServiceCollection services)
    {
        services.AddHttpClient<IClient<BarcodeResponse>, BarcodeClient>();
        services.AddHttpClient<IClient<FreshFoodResponse>, FreshFoodsClient>();
    }

    private static void AddScoped(this IServiceCollection services)
    {
        services.AddScoped<IAuth, Authenticator>();
        services.AddScoped<IBarcodeService, BarcodeFoodService>();
        services.AddScoped<IFreshFoodsService, FreshFoodService>();
    }
}
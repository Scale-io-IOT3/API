using System.Text;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.Interface;
using Core.Interface.Foods;
using Infrastructure.Clients;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Services.Foods;
using Infrastructure.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(configuration);
        services.AddDbContext<AppDbContext>(options => { options.UseSqlite(configuration["DB_CONNECTION_STRING"]); });
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

    private static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = configuration["JwtConfig:Issuer"],
                ValidAudience = configuration["JwtConfig:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtConfig:Key"]!)),
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };

            services.AddAuthorization();
        });
    }
}
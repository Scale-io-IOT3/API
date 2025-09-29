using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.Interface;
using Core.Interface.Foods;
using Core.Interface.Login;
using Core.Models.API;
using Core.Models.Entities;
using Infrastructure.Clients;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Repositories;
using Infrastructure.Services.Foods;
using Infrastructure.Services.Login;
using Infrastructure.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TokenHandler = Infrastructure.Utils.TokenHandler;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddAuthentication(configuration);
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
        services.AddScoped<AppDbContext>();
        services.AddRepositories();
        services.AddServices();
    }

    private static void AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuth, Authenticator>();
        services.AddScoped<ITokenHandler, TokenHandler>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IBarcodeService, BarcodeFoodService>();
        services.AddScoped<IFreshFoodsService, FreshFoodService>();
    }

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IRepo<User>, UserRepository>();
        services.AddScoped<IRepo<Token>, TokenRepository>();
    }

    private static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var jwtOptions = jwtSection.Get<JwtOptions>()!;
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
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Convert.FromBase64String(jwtOptions.Key)
                ),
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
        });
        services.AddAuthorization();
    }
}
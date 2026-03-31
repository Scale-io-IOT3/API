using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Core.Interface.Foods;
using Core.Interface.Login;
using Core.Interface.Meals;
using Core.Models.API;
using Core.Models.Entities;
using Infrastructure.Clients;
using Infrastructure.Clients.Foods;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Repositories;
using Infrastructure.Services.Foods;
using Infrastructure.Services.Login;
using Infrastructure.Services.Meals;
using Infrastructure.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using TokenHandler = Infrastructure.Utils.TokenHandler;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()!;
        var signingKeyBytes = ResolveSigningKey(jwtOptions, configuration);
        var signingKey = new SymmetricSecurityKey(signingKeyBytes);

        services.AddSingleton(signingKey);
        services.AddAuthentication(configuration, signingKey);
        services.AddMemoryCache();
        services.AddPersistence(configuration);
        services.AddClients();
        services.AddHealthChecks();
    }

    private static void AddClients(this IServiceCollection services)
    {
        services.AddHttpClient<IClient<BarcodeResponse>, BarcodeClient>(ConfigureOpenFoodHttpClient)
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
        services.AddHttpClient<IClient<FreshFoodResponse>, FreshFoodsClient>(ConfigureUsdaHttpClient)
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
        services.AddHttpClient<IClient<OpenFoodSearchResponse>, OpenFoodSearchClient>(ConfigureOpenFoodHttpClient)
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
        services.AddHttpClient<IGtinSearchClient, GtinSearchClient>(ConfigureGtinHttpClient)
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var source = ResolveConnectionString(configuration);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(source));
        services.AddRepositories();
        services.AddServices();
    }

    private static void AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuth, Authenticator>();
        services.AddScoped<ITokenHandler, TokenHandler>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBarcodeService, ConsensusBarcodeService>();
        services.AddScoped<IFreshFoodsService, ConsensusFreshFoodsService>();
        services.AddScoped<IMealsService, MealServie>();
    }

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IRepo<User>, UserRepository>();
        services.AddScoped<MealRepository>();
        services.AddScoped<IRepo<Meal>>(provider => provider.GetRequiredService<MealRepository>());
        services.AddScoped<IRepo<Token>, TokenRepository>();
    }

    private static void AddAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        SymmetricSecurityKey signingKey
    )
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
                IssuerSigningKey = signingKey,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = JwtRegisteredClaimNames.Name
            };
        });
        services.AddAuthorization();
    }

    private static void ConfigureHttpClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(6);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Scale.io_API/1.0");
    }

    private static void ConfigureUsdaHttpClient(HttpClient client)
    {
        ConfigureHttpClient(client);
        client.Timeout = TimeSpan.FromMilliseconds(2500);
    }

    private static void ConfigureOpenFoodHttpClient(HttpClient client)
    {
        ConfigureHttpClient(client);
        client.Timeout = TimeSpan.FromMilliseconds(600);
    }

    private static void ConfigureGtinHttpClient(HttpClient client)
    {
        ConfigureHttpClient(client);
        client.Timeout = TimeSpan.FromMilliseconds(900);
    }

    private static AsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(message => message.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(1, _ => TimeSpan.FromMilliseconds(150));
    }

    private static AsyncPolicy<HttpResponseMessage> GetSourceCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(message => message.StatusCode == HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(1, TimeSpan.FromMinutes(2));
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var source = GetConnectionString(configuration);

        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException(
            "Database connection string was not configured. Set SOURCE (or DATABASE_URL) or ConnectionStrings:DefaultConnection."
        );

        return source;
    }

    private static string? GetConnectionString(IConfiguration configuration)
    {
        return configuration["DATABASE_URL"] ??
        configuration["SOURCE"] ??
        configuration.GetConnectionString("DefaultConnection");
    }

    private static byte[] ResolveSigningKey(JwtOptions options, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(options.Key))
        {
            try
            {
                var decoded = Convert.FromBase64String(options.Key);
                if (decoded.Length >= 32) return decoded;
            }
            catch (FormatException)
            {
                // Continue to fallback behavior.
            }
        }

        var isDev = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"],
            "Development",
            StringComparison.OrdinalIgnoreCase
        );

        if (isDev) return RandomNumberGenerator.GetBytes(64);
        throw new InvalidOperationException("Jwt:Key must be set to a base64-encoded key with at least 32 bytes.");
    }
}

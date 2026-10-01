using Core.Interface;
using Core.Interface.Foods;
using Core.Interface.Login;
using Core.Interface.Meals;
using Core.Models.Entities;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Repositories;
using Infrastructure.Services.Foods;
using Infrastructure.Services.Login;
using Infrastructure.Services.Meals;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TokenHandler = Infrastructure.Utils.TokenHandler;

namespace Infrastructure;

public static partial class DependencyInjection
{
    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((provider, options) => options.UseNpgsql(
            ResolveConnectionString(provider.GetRequiredService<IConfiguration>())));
        services.AddRepositories();
        services.AddServices();
    }

    private static void AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuth, Authenticator>();
        services.AddScoped<ITokenHandler, TokenHandler>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IBarcodeService, ConsensusBarcodeService>();
        services.AddSingleton<IFreshFoodsService, ConsensusFreshFoodsService>();
        services.AddScoped<IMealsService, MealServie>();
    }

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IRepo<User>, UserRepository>();
        services.AddScoped<MealRepository>();
        services.AddScoped<IRepo<Meal>>(provider => provider.GetRequiredService<MealRepository>());
        services.AddScoped<ITokenRepository, TokenRepository>();
        services.AddScoped<IRepo<Token>>(provider => provider.GetRequiredService<ITokenRepository>());
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var source = GetConnectionString(configuration);

        if (string.IsNullOrWhiteSpace(source)) throw new InvalidOperationException(
            "Database connection string was not configured. Set SOURCE (or DATABASE_URL) or ConnectionStrings:DefaultConnection."
        );

        return NormalizeConnectionString(source);
    }

    private static string? GetConnectionString(IConfiguration configuration)
    {
        return configuration["DATABASE_URL"] ??
        configuration["SOURCE"] ??
        configuration.GetConnectionString("DefaultConnection");
    }

    private static string NormalizeConnectionString(string raw)
    {
        var source = raw.Trim();
        if (IsPostgresUri(source))
        {
            return ConvertPostgresUri(source);
        }

        try
        {
            return new NpgsqlConnectionStringBuilder(source).ConnectionString;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Invalid database connection string. Use either Npgsql format " +
                "('Host=...;Port=...;Database=...;Username=...;Password=...') or " +
                "Postgres URL format ('postgres://user:pass@host:5432/db?sslmode=require').",
                ex
            );
        }
    }

    private static bool IsPostgresUri(string source)
    {
        return source.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
               source.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
    }

    private static string ConvertPostgresUri(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("DATABASE_URL is not a valid URI.");
        }

        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');

        if (string.IsNullOrWhiteSpace(uri.Host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException(
                "DATABASE_URL is missing host, username, or database name. " +
                "Expected format: postgres://user:pass@host:5432/db?sslmode=require"
            );
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = username,
            Password = password,
            Database = database
        };

        ApplyQueryParameters(uri.Query, builder);
        return builder.ConnectionString;
    }

    private static void ApplyQueryParameters(string query, NpgsqlConnectionStringBuilder builder)
    {
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.None);
            var key = Uri.UnescapeDataString(kv[0]);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"DATABASE_URL query parameter '{key}' has no value. Example: '?sslmode=require'."
                );
            }

            var normalizedKey = key.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            if (normalizedKey is "sslmode" or "ssl")
            {
                if (!Enum.TryParse<SslMode>(value, true, out var sslMode))
                {
                    throw new InvalidOperationException(
                        $"Unsupported sslmode '{value}'. Valid values include Disable, Prefer, Require, VerifyCA, VerifyFull."
                    );
                }

                builder.SslMode = sslMode;
                continue;
            }

            try
            {
                builder[key] = value;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unsupported DATABASE_URL query parameter '{key}'.",
                    ex
                );
            }
        }
    }

}

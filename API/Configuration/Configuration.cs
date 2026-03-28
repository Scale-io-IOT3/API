using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Core;
using Core.Models.Entities;
using DotNetEnv;
using Infrastructure;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Infrastructure.Utils;

namespace Scale.io_API.Configuration;

public static class Configuration
{
    private const string DefaultUsername = "Monke";
    private const string DefaultPassword = "1234567890Abc!";

    public static void Configure(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.ConfigureStartupExperience();
        app.MapHealthChecks("/health");
    }

    private static void MapDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    public static void Configure(this WebApplicationBuilder builder)
    {
        builder.EnvironmentConfig();
        builder.ServicesConfig();
    }

    private static void ServicesConfig(this WebApplicationBuilder webAppBuilder)
    {
        webAppBuilder.Services.AddCore();
        webAppBuilder.Services.AddInfrastructure(webAppBuilder.Configuration);

        webAppBuilder.Services.AddApiVersioning(options =>
        {
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.DefaultApiVersion = new ApiVersion(1);
            options.ReportApiVersions = true;
            options.ApiVersionReader = new HeaderApiVersionReader("X-api-version");
        }).AddMvc().AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = false;
        });
        webAppBuilder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
        });

        webAppBuilder.Services.AddDataProtection().UseCryptographicAlgorithms(
            new AuthenticatedEncryptorConfiguration
            {
                EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
                ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
            }
        );

        webAppBuilder.Services.AddOpenApi();
    }

    private static void EnvironmentConfig(this WebApplicationBuilder webApplicationBuilder)
    {
        if (webApplicationBuilder.Environment.IsDevelopment()) Env.Load();
        webApplicationBuilder.Configuration.AddEnvironmentVariables();
    }

    private static void ConfigureStartupExperience(this WebApplication app)
    {
        if (ShouldApplyMigrations(app)) ApplyMigrations(app);
        if (ShouldMapDocumentation(app)) app.MapDocumentation();
        SeedDefaultUser(app);
    }

    private static bool ShouldApplyMigrations(WebApplication app)
    {
        var configured = app.Configuration.GetValue<bool?>("ApplyMigrationsOnStartup");
        return configured ?? app.Environment.IsDevelopment();
    }

    private static void ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    private static bool ShouldMapDocumentation(WebApplication app)
    {
        return app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableApiDocs");
    }

    private static void SeedDefaultUser(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = db.Users.SingleOrDefault(u => u.Username == DefaultUsername);
        var created = false;
        if (user is null)
        {
            user = new User
            {
                Username = DefaultUsername,
                PasswordHash = ""
            };
            db.Users.Add(user);
            created = true;
        }

        user.PasswordHash = Cryptography.Hash(DefaultPassword, user);
        db.SaveChanges();

        app.Logger.LogInformation(
            "Default user {Username} {Action}.",
            DefaultUsername,
            created ? "created" : "updated"
        );
    }
}

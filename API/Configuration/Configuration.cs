using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Core;
using DotNetEnv;
using Infrastructure;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace Scale.io_API.Configuration;

public static class Configuration
{
    public static void Configure(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.DevConfig();
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
        Env.Load();
        webApplicationBuilder.Configuration.AddEnvironmentVariables();
    }

    private static void DevConfig(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        app.MapDocumentation();
    }
}
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Core;
using DotNetEnv;
using Infrastructure;
using Scalar.AspNetCore;

namespace Scale.io_API.Configuration;

public static class Configuration
{
    public static void Configure(this WebApplication app)
    {
        app.DevConfig();
        app.UseAuthorization();
        app.MapControllers();
    }

    private static void MapDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    public static void Configure(this WebApplicationBuilder builder)
    {
        builder.ServicesConfig();
        builder.EnvironmentConfig();
    }

    private static void ServicesConfig(this WebApplicationBuilder webAppBuilder)
    {
        webAppBuilder.Services.AddCore();
        webAppBuilder.Services.AddInfrastructure();

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

        webAppBuilder.Services.AddOpenApi();
    }

    private static void EnvironmentConfig(this WebApplicationBuilder webApplicationBuilder)
    {
        Env.Load();
        webApplicationBuilder.Configuration.AddEnvironmentVariables();
    }

    private static void DevConfig(this WebApplication app)
    {
        app.UseHttpsRedirection();
        if (app.Environment.IsDevelopment()) app.MapDocumentation();
    }
}
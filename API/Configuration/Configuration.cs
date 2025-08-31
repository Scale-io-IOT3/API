using System.Text.Json;
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
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
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
            options.DefaultApiVersion = new ApiVersion(1);
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-api-version")
            );
        }).AddMvc().AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });
        webAppBuilder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        });

        webAppBuilder.Services.AddOpenApi();
    }

    private static void EnvironmentConfig(this WebApplicationBuilder webApplicationBuilder)
    {
        Env.Load();
        webApplicationBuilder.Configuration.AddEnvironmentVariables();
    }
}
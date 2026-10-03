using System.Text.Json;
using Asp.Versioning;
using Core;
using DotNetEnv;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

namespace Scale.io_API.Configuration;

public static class Configuration
{
    public static void Configure(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }
        else if (app.Configuration.GetValue<bool>("EnableApiDocs"))
        {
            app.MapOpenApi()
                .RequireAuthorization();
        }
    }

    public static void Configure(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment()) Env.Load();
        builder.Configuration.AddEnvironmentVariables();
        builder.Services.AddCore();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddProblemDetails();

        builder.Services.AddApiVersioning(options =>
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

        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        });
        builder.Services.AddOpenApi();
    }
}

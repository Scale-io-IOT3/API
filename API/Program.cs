using System.Text.Json;
using Scalar.AspNetCore;
using Asp.Versioning;
using Core;
using DotNetEnv;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);
ServicesConfig(builder);

var application = builder.Build();
EnvironmentConfig(builder);
AppConfig(application);

application.Run();
return;

void ServicesConfig(WebApplicationBuilder webAppBuilder)
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

void AppConfig(WebApplication app)
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

void EnvironmentConfig(WebApplicationBuilder webApplicationBuilder)
{
    Env.Load();
    webApplicationBuilder.Configuration.AddEnvironmentVariables();
}
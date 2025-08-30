using System.Text.Json;
using Scalar.AspNetCore;
using Asp.Versioning;
using Core;
using DotNetEnv;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);
ServicesConfig(builder);

var app = builder.Build();
EnvironmentConfig(builder);
AppConfig(app);

app.Run();
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
void AppConfig(WebApplication webApp)
{
    if (webApp.Environment.IsDevelopment())
    {
        webApp.MapOpenApi();
        webApp.MapScalarApiReference();
    }

    webApp.UseHttpsRedirection();
    webApp.UseAuthorization();
    webApp.MapControllers();
}
void EnvironmentConfig(WebApplicationBuilder webApplicationBuilder)
{
    Env.Load();
    webApplicationBuilder.Configuration.AddEnvironmentVariables();
}
using System.Net;
using Core.DTO.Barcodes;
using Core.DTO.FreshFoods;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Core.Interface.Foods;
using Infrastructure.Clients;
using Infrastructure.Clients.Foods;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Infrastructure;

public static partial class DependencyInjection
{
    private static void AddClients(this IServiceCollection services)
    {
        services.AddHttpClient<IClient<BarcodeResponse>, BarcodeClient>(ConfigureOpenFoodHttpClient)
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
        services.AddHttpClient<IClient<FreshFoodResponse>, FreshFoodsClient>(ConfigureUsdaHttpClient)
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
        services.AddHttpClient<IClient<OpenFoodSearchResponse>, OpenFoodSearchClient>(ConfigureOpenFoodHttpClient)
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
        services.AddHttpClient<IClient<OpenFoodSearchALiciousResponse>, OpenFoodSearchALiciousClient>(ConfigureOpenFoodHttpClient)
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
        services.AddHttpClient<IGtinSearchClient, GtinSearchClient>(ConfigureGtinHttpClient)
            .AddPolicyHandler(GetSourceCircuitBreakerPolicy());
    }

    private static void ConfigureHttpClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(6);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Scale.io_API/1.0");
    }

    private static void ConfigureUsdaHttpClient(HttpClient client)
    {
        ConfigureHttpClient(client);
        client.Timeout = TimeSpan.FromMilliseconds(4000);
    }

    private static void ConfigureOpenFoodHttpClient(HttpClient client)
    {
        ConfigureHttpClient(client);
        client.Timeout = TimeSpan.FromMilliseconds(2000);
    }

    private static void ConfigureGtinHttpClient(HttpClient client)
    {
        ConfigureHttpClient(client);
        client.Timeout = TimeSpan.FromMilliseconds(6000);
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
            .CircuitBreakerAsync(2, TimeSpan.FromMinutes(2));
    }

}

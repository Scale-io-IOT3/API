using System.Net.Http.Json;
using Core.DTO.FreshFoods;
using Core.Entities;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Clients;

public class FoodsClient : CustomClient<FreshFoodResponse>
{
    private readonly string _baseUrl = "https://api.nal.usda.gov/fdc/v1/foods/search";

    public FoodsClient(HttpClient client, IConfiguration configuration) : base(client)
    {
        var key = configuration["API_KEY"];
        _baseUrl = $"{_baseUrl}?api_key={key}";
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Missing API key configuration.");
    }
    
    public override Task<FreshFoodResponse?> Fetch(string food)
    {
        var url = $"{_baseUrl}&query={Uri.EscapeDataString(food)}&dataType=SR%20Legacy";
        return GetFromApiAsync(url);
    }
}
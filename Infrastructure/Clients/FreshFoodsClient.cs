using Core.DTO.FreshFoods;
using Infrastructure.Clients.Abstract;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Clients;

public class FreshFoodsClient(HttpClient client, IConfiguration config) : AuthClient<FreshFoodResponse>(client, config)
{
    private const string Url = "https://api.nal.usda.gov/fdc/v1/foods/search";
    protected override string Request(string url) => $"{Url}?api_key={Key()}&query={base.Request(url)}&dataType=SR%20Legacy";
}
using Core.DTO.FreshFoods;
using Core.Interface;
using Infrastructure.Clients.Abstract;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Clients;

public class FreshFoodsClient(HttpClient client, IAuth auth) : AuthClient<FreshFoodResponse>(client, auth)
{
    private const string Url = "https://api.nal.usda.gov/fdc/v1/foods/search";
    protected override string Request(string url)
    {
        return $"{Url}?api_key={Key()}&query={base.Request(url)}&dataType=SR%20Legacy&pageSize=25";
    }
}

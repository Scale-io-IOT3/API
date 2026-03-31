using Core.DTO.OpenFoodFacts;
using Infrastructure.Clients.Abstract;

namespace Infrastructure.Clients.Foods;

public class OpenFoodSearchClient(HttpClient client) : Client<OpenFoodSearchResponse>(client)
{
    private const string Url = "https://world.openfoodfacts.org/cgi/search.pl";

    protected override string Request(string url)
    {
        return
            $"{Url}?search_terms={base.Request(url)}&search_simple=1&action=process&json=1&page_size=25&fields=product_name,brands,nutriments";
    }
}

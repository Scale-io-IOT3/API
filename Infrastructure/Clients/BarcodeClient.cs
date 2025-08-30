using Core.DTO;
using System.Net.Http.Json;
using Core.Entities;

namespace Infrastructure.Clients;

public class BarcodeClient(HttpClient client) : CustomClient(client)
{
    private static string Url => "https://world.openfoodfacts.net/api/v2/product/";

    public Task<BarcodeResponse?> FetchProduct(string barcode)
    {
        var url = $"{Url}{Uri.EscapeDataString(barcode)}.json";
        return GetFromApiAsync<BarcodeResponse>(url);
    }
}
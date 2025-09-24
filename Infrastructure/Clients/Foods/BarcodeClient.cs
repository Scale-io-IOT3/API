using Core.DTO.Barcodes;
using Infrastructure.Clients.Abstract;

namespace Infrastructure.Clients;

public class BarcodeClient(HttpClient client) : Client<BarcodeResponse>(client)
{
    private static string Url => "https://world.openfoodfacts.net/api/v2/product/";
    protected override string Request(string url) => $"{Url}{base.Request(url)}.json";
}
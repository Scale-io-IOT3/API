using Core.DTO.Barcodes;
using Infrastructure.Clients.Abstract;

namespace Infrastructure.Clients.Foods;

public class BarcodeClient(HttpClient client) : Client<BarcodeResponse>(client)
{
    private static string Url => "https://world.openfoodfacts.org/api/v2/product/";

    protected override string Request(string url)
    {
        return $"{Url}{base.Request(url)}.json";
    }
}
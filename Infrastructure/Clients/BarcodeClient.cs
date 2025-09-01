using Core.DTO.Barcodes;
using Core.Entities;

namespace Infrastructure.Clients;

public class BarcodeClient(HttpClient client) : CustomClient<BarcodeResponse>(client)
{
    private static string Url => "https://world.openfoodfacts.net/api/v2/product/";


    public override Task<BarcodeResponse?> Fetch(string barcode)
    {
        var url = $"{Url}{Uri.EscapeDataString(barcode)}.json";
        return GetFromApiAsync(url);
    }
}
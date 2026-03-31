using System.Text.Json;
using Core.DTO.GtinSearch;
using Core.Interface.Foods;

namespace Infrastructure.Clients.Foods;

public class GtinSearchClient(HttpClient client) : IGtinSearchClient
{
    private const string BaseUrl = "https://www.gtinsearch.org/api/items";

    public Task<GtinSearchItem[]> LookupBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDigits(barcode);
        var url = $"{BaseUrl}/{Uri.EscapeDataString(normalized)}";
        return GetItems(url, cancellationToken);
    }

    public Task<GtinSearchItem[]> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?query={Uri.EscapeDataString(query)}";
        return GetItems(url, cancellationToken);
    }

    private async Task<GtinSearchItem[]> GetItems(string url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode >= 500 || (int)response.StatusCode == 429)
            {
                throw new HttpRequestException(
                    $"GTINSearch responded with {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode
                );
            }

            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement
                .EnumerateArray()
                .Select(DeserializeItem)
                .Where(item => item is not null)
                .Cast<GtinSearchItem>()
                .ToArray(),
            JsonValueKind.Object => DeserializeItem(document.RootElement) is { } single ? [single] : [],
            _ => []
        };
    }

    private static GtinSearchItem? DeserializeItem(JsonElement element)
    {
        try
        {
            return JsonSerializer.Deserialize<GtinSearchItem>(element.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeDigits(string input)
    {
        return new string(input.Where(char.IsDigit).ToArray());
    }
}

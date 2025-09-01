using System.Net.Http.Json;
using Core.Interface;

namespace Core.Entities;

public abstract class CustomClient<T>(HttpClient client) : IApiClient<T> where T : ISourceResponse
{
    protected async Task<T?> GetFromApiAsync(string url)
    {
        var httpResponse = await client.GetAsync(url);
        if (!httpResponse.IsSuccessStatusCode) return default;

        return await httpResponse.Content.ReadFromJsonAsync<T>();
    }

    public abstract Task<T?> Fetch(string barcode);
}

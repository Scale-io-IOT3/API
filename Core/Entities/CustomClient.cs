using System.Net.Http.Json;

namespace Core.Entities;

public abstract class CustomClient(HttpClient client)
{
    protected async Task<T?> GetFromApiAsync<T>(string url)
    {
        var httpResponse = await client.GetAsync(url);
        if (!httpResponse.IsSuccessStatusCode) return default;

        return await httpResponse.Content.ReadFromJsonAsync<T>();
    }
}

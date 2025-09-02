using System.Net.Http.Json;
using Core.Interface;

namespace Infrastructure.Clients.Abstract;

public abstract class Client<T>(HttpClient client) : IClient<T> where T : IResponse
{
    private async Task<T?> GetFromSource(string url)
    {
        var response = await client.GetAsync(
            Request(url)
        );
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
    }

    public Task<T?> Fetch(string url) => GetFromSource(url);
    protected virtual string Request(string url) => Uri.EscapeDataString(url);
}
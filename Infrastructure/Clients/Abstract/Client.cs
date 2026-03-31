using System.Net.Http.Json;
using Core.Interface;

namespace Infrastructure.Clients.Abstract;

public abstract class Client<T>(HttpClient client) : IClient<T> where T : IResponse
{
    private async Task<T?> GetFromSource(string url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            Request(url),
            cancellationToken
        );

        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<T>(cancellationToken);

        if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException(
                $"Upstream responded with {(int)response.StatusCode} ({response.StatusCode}).",
                null,
                response.StatusCode
            );
        }

        return default;
    }

    public Task<T?> Fetch(string url, CancellationToken cancellationToken = default) => GetFromSource(url, cancellationToken);
    protected virtual string Request(string url) => Uri.EscapeDataString(url);
}

using System.Net.Http.Json;
using System.Text.Json;
using Core.Interface;

namespace Infrastructure.Clients.Abstract;

public abstract class Client<T>(HttpClient client) : IClient<T> where T : IResponse
{
    private async Task<T?> GetFromSource(string url, CancellationToken cancellationToken)
    {
        HttpRequestException? lastTransientException = null;
        Exception? lastParseException = null;

        foreach (var requestUrl in Requests(url))
        {
            using var response = await client.GetAsync(requestUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                    if (payload is not null)
                    {
                        return payload;
                    }
                }
                catch (JsonException ex)
                {
                    lastParseException = ex;
                }
                catch (NotSupportedException ex)
                {
                    lastParseException = ex;
                }

                continue;
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                lastTransientException = new HttpRequestException(
                    $"Upstream responded with {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode
                );
            }
        }

        if (lastTransientException is not null)
        {
            throw lastTransientException;
        }

        if (lastParseException is not null)
        {
            throw new HttpRequestException("Upstream returned an unexpected payload format.", lastParseException);
        }

        return default;
    }

    public Task<T?> Fetch(string url, CancellationToken cancellationToken = default) => GetFromSource(url, cancellationToken);

    protected virtual IEnumerable<string> Requests(string url)
    {
        yield return Request(url);
    }

    protected virtual string Request(string url) => Uri.EscapeDataString(url);
}

using Core.Interface;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Clients.Abstract;

public abstract class AuthClient<T>(HttpClient client, IConfiguration config) : Client<T>(client) where T : IResponse
{
    protected string Key() => GetKey() ?? throw new InvalidOperationException("Can't get the API key.");

    private string? GetKey()
    {
        var key = config["API_KEY"];
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }
}
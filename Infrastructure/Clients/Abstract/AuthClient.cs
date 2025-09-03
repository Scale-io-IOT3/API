using Core.Interface;
using Infrastructure.Utils;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Clients.Abstract;

public abstract class AuthClient<T>(HttpClient client, IAuth authenticator) : Client<T>(client) where T : IResponse
{
    protected string Key() => authenticator.Key() ?? throw new InvalidOperationException("Can't retrieve the API key.");
}
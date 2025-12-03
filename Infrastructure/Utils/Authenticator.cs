using Core.Interface;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Utils;

public class Authenticator(IConfiguration configuration) : IAuth
{
    public string? Key() => IsEmpty(configuration["API_KEY"]) ? null : configuration["API_KEY"];
    private static bool IsEmpty(string? s) => string.IsNullOrWhiteSpace(s);
}
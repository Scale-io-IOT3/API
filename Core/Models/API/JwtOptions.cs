namespace Core.Models.API;

public class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";
    public int TokenValidityMins { get; init; }
}
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Core.DTO;
using Core.Interface.Login;
using Core.Models.API;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Utils;

public class TokenHandler(IOptions<JwtOptions> options) : ITokenHandler
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(string username, out int expiresIn)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_options.TokenValidityMins);
        var descriptor = BuildDescriptor(username, expiry);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);

        expiresIn = (int)(expiry - DateTime.UtcNow).TotalSeconds;
        return handler.WriteToken(token);
    }

    private SecurityTokenDescriptor BuildDescriptor(string username, DateTime expiry)
    {
        return new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Name, username)]),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiry,
            SigningCredentials = GetSigningCredentials()
        };
    }

    private SigningCredentials GetSigningCredentials()
    {
        var keyBytes = Convert.FromBase64String(_options.Key);
        var key = new SymmetricSecurityKey(keyBytes);
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
    }
}
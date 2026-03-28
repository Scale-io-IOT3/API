using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Core.Interface;
using Core.Interface.Login;
using Core.Models.API;
using Core.Models.API.Responses;
using Core.Models.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Utils;

public class TokenHandler(IOptions<JwtOptions> options, IRepo<Token> repo) : ITokenHandler
{
    private readonly JwtOptions _options = options.Value;

    public async Task<TokenResponse> Create(User user)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_options.TokenValidityMins);
        return await Generate(user, expiry);
    }

    public async Task<TokenResponse?> Refresh(string token)
    {
        var t = await repo.Find(token);
        return t is null || t.Expired() ? null : await Rotate(t);
    }

    private async Task<TokenResponse> Generate(User user, DateTime expiry)
    {
        var response = GenerateResponse(user, expiry);
        var token = Token.From(response, user.Id);
        token.ExpiresAt = DateTime.UtcNow.AddDays(30);

        if (!token.Expired()) await repo.CreateOrUpdate(token);

        return response;
    }

    private string GenerateAccessToken(User user, DateTime expiry)
    {
        var descriptor = BuildDescriptor(user, expiry);
        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.CreateToken(descriptor);

        return handler.WriteToken(accessToken);
    }

    private SecurityTokenDescriptor BuildDescriptor(User user, DateTime expiry)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.Username)
        };

        return new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
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

    private async Task<TokenResponse> Rotate(Token token)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_options.TokenValidityMins);
        var response = GenerateResponse(token.User, expiry);

        token.TokenHash = response.RefreshToken;
        token.TokenFingerprint = response.RefreshToken;
        token.ExpiresAt = DateTime.UtcNow.AddDays(30);
        await repo.CreateOrUpdate(token);

        return response;
    }

    private TokenResponse GenerateResponse(User user, DateTime expiry)
    {
        return new TokenResponse
        {
            RefreshToken = Cryptography.GenerateToken(),
            AccessToken = GenerateAccessToken(user, expiry)
        };
    }
}

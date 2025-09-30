using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

    public async Task<LoginResponse> Create(User user)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_options.TokenValidityMins);
        return await Generate(user, expiry);
    }

    public async Task<RefreshResponse?> Refresh(string token)
    {
        var t = await repo.Find(token);
        if (t == null) return null;

        return await Rotate(t);
    }

    private async Task<LoginResponse> Generate(User user, DateTime expiry)
    {
        var response = new LoginResponse
        {
            Username = user.Username,
            AccessToken = GenerateAccessToken(user, expiry),
            RefreshToken = GetRefreshToken()
        };

        var token = Token.From(response, user.Id);
        if (!token.Expired()) await repo.CreateOrUpdate(token);

        return response;
    }

    private string GenerateAccessToken(User user, DateTime expiry)
    {
        var descriptor = BuildDescriptor(user.Username, expiry);
        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.CreateToken(descriptor);

        return handler.WriteToken(accessToken);
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

    private static string GetRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);

        return Convert.ToBase64String(randomBytes);
    }

    private async Task<RefreshResponse> Rotate(Token token)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_options.TokenValidityMins);
        var response = new RefreshResponse
        {
            Refresh = GetRefreshToken(),
            Access = GenerateAccessToken(token.User, expiry)
        };

        token.Refresh = response.Refresh;
        await repo.CreateOrUpdate(token);

        return response;
    }
}
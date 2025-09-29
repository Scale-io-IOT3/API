using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Core.Interface;
using Core.Interface.Login;
using Core.Models.API;
using Core.Models.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Utils;

public class TokenHandler(IOptions<JwtOptions> options, IRepo<Token> repo) : ITokenHandler
{
    private readonly JwtOptions _options = options.Value;

    public async Task<Token> GetOrCreate(User user)
    {
        var token = await repo.FindById(user.Id);
        if (token is null || token.RefreshExpiry < DateTime.UtcNow) return await CreateToken(user);

        return token;
    }

    private async Task<Token> CreateToken(User user)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_options.TokenValidityMins);

        var descriptor = BuildDescriptor(user.Username, expiry);
        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.CreateToken(descriptor);

        var token = new Token
        {
            Id = user.Id,
            Access = handler.WriteToken(accessToken),
            Refresh = await GetRefreshToken(user.Id),
            AccessExpiry = expiry
        };

        if (token.RefreshExpiry >= DateTime.UtcNow) await repo.CreateOrUpdate(token);

        return token;
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

    private async Task<string> GetRefreshToken(int id)
    {
        var token = await repo.FindById(id);
        if (token is not null && token.RefreshExpiry >= DateTime.UtcNow) return token.Refresh;

        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
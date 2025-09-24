using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.Interface.Login;
using Core.Models.API.Requests;
using Core.Models.API.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Infrastructure.Services.Login;

public class LoginService(IConfiguration configuration) : ILoginService
{
    private readonly string _audience = configuration["Jwt:Audience"]!;

    private readonly DateTime _expiry = DateTime.UtcNow.AddMinutes(
        configuration.GetValue<int>("Jwt:TokenValidityMins")
    );

    private readonly string _issuer = configuration["Jwt:Issuer"]!;
    private readonly string _key = configuration["Jwt:Issuer"]!;

    public async Task<LoginResponse?> Authenticate(LoginRequest request)
    {
        var desc = GenerateDescriptor(request);
        return new LoginResponse
        {
            AccessToken = CreateToken(desc),
            ExpiresIn = (int)_expiry.Subtract(DateTime.UtcNow).TotalSeconds,
            User = request.Username
        };
    }

    private SecurityTokenDescriptor GenerateDescriptor(LoginRequest request)
    {
        return new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Name, request.Username)
            ]),
            Issuer = _issuer,
            Audience = _audience,
            Expires = _expiry,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(configuration["Jwt:Key"]!)),
                SecurityAlgorithms.HmacSha256Signature
            )
        };
    }

    private static string CreateToken(SecurityTokenDescriptor descriptor)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(
            handler.CreateToken(descriptor)
        );
    }
}
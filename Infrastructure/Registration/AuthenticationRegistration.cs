using System.IdentityModel.Tokens.Jwt;
using Core.Models.API;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure;

public static partial class DependencyInjection
{
    private static void AddApiAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer) &&
                                 !string.IsNullOrWhiteSpace(options.Audience) && options.TokenValidityMins > 0,
                "Jwt:Issuer, Audience and a positive TokenValidityMins are required.")
            .Validate(options => IsValidSigningKey(options.Key),
                "Jwt:Key must be base64-encoded with at least 32 bytes.")
            .ValidateOnStart();
        services.AddSingleton(provider => new SymmetricSecurityKey(Convert.FromBase64String(
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value.Key)));
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>, SymmetricSecurityKey>((options, jwt, signingKey) =>
        {
            var jwtOptions = jwt.Value;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = signingKey,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = JwtRegisteredClaimNames.Name
            };
        });
        services.AddAuthorization();
    }

    private static bool IsValidSigningKey(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                return Convert.FromBase64String(key).Length >= 32;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return false;
    }
}

using Core.Models.API;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData("Jwt:Key", "")]
    [InlineData("Jwt:Key", "invalid-base64")]
    [InlineData("Jwt:Key", "c2hvcnQ=")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:TokenValidityMins", "0")]
    public void InvalidJwtConfigurationIsRejected(string name, string value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "api", ["Jwt:Audience"] = "mobile",
            ["Jwt:Key"] = Convert.ToBase64String(new byte[64]), ["Jwt:TokenValidityMins"] = "60"
        };
        values[name] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }
}

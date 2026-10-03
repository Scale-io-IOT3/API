using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Tests;

public sealed class ApiDocumentationTests
{
    [Fact]
    public async Task DevelopmentOpenApiDocumentAndScalarRemainPublic()
    {
        using var factory = new ApiFactory(environment: "Development");
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal("3.1.1", document.GetProperty("openapi").GetString());
        Assert.True(document.GetProperty("paths").TryGetProperty("/Auth", out _));
        Assert.True(document.GetProperty("paths").TryGetProperty("/Meals", out _));

        var response = await client.GetAsync("/scalar/v1");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("openapi/v1.json", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task EnabledNonDevelopmentOpenApiRequiresAuthenticationAndScalarIsUnavailable(string environment)
    {
        using var factory = new ApiFactory(enableApiDocs: true, environment: environment);
        factory.Initialize();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scalar/v1")).StatusCode);

        var login = await client.PostAsJsonAsync("/Auth", new { username = "tester", password = ApiFactory.Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            tokens.GetProperty("access_token").GetString());
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal("3.1.1", document.GetProperty("openapi").GetString());
        Assert.True(document.GetProperty("paths").TryGetProperty("/Auth", out _));
        Assert.True(document.GetProperty("paths").TryGetProperty("/Meals", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scalar/v1")).StatusCode);
    }

    [Fact]
    public async Task DisabledNonDevelopmentDocumentationIsUnavailable()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scalar/v1")).StatusCode);
    }
}

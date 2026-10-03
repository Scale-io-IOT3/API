using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Tests;

public sealed class ApiDocumentationTests
{
    [Fact]
    public async Task OpenApiDocumentAndScalarRemainAvailable()
    {
        using var factory = new ApiFactory(enableApiDocs: true);
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
}

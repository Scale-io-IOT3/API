using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Repositories;
using Infrastructure.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests;

public sealed class ContractTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ContractTests()
    {
        _factory.Initialize();
        _client = _factory.CreateClient();
    }

    private async Task<JsonElement> Login()
    {
        var response = await _client.PostAsJsonAsync("/Auth", new { username = "tester", password = ApiFactory.Password });
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        return tokens;
    }

    [Fact]
    public async Task LoginForUnknownUserReturnsProblem()
    {
        var response = await _client.PostAsJsonAsync("/Auth", new { username = "absent", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProtectedEndpointsRequireAuthentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/Meals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/Search/apple")).StatusCode);
    }

    [Fact]
    public async Task RefreshRotatesAndRejectsOldToken()
    {
        var tokens = await Login();
        var old = tokens.GetProperty("refresh_token").GetString();
        var response = await _client.PostAsJsonAsync("/Auth/refresh", new { token = old });
        response.EnsureSuccessStatusCode();
        var rotated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(old, rotated.GetProperty("refresh_token").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/Auth/refresh", new { token = old })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Tokens.SingleAsync();
        Assert.NotEqual(rotated.GetProperty("refresh_token").GetString(), stored.TokenHash);
        Assert.True(Cryptography.Verify(rotated.GetProperty("refresh_token").GetString()!, stored.TokenHash));
    }

    [Fact]
    public async Task StaleRefreshSnapshotCannotOverwriteReplacement()
    {
        var tokens = await Login();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repo = new TokenRepository(db);
        var current = (await repo.Find(tokens.GetProperty("refresh_token").GetString()!))!;
        var first = new Core.Models.Entities.Token
        {
            TokenHash = Cryptography.Hash("first"), TokenFingerprint = Cryptography.FingerprintToken("first")
        };
        var second = new Core.Models.Entities.Token
        {
            TokenHash = Cryptography.Hash("second"), TokenFingerprint = Cryptography.FingerprintToken("second")
        };
        Assert.True(await repo.TryRotate(current, first));
        Assert.False(await repo.TryRotate(current, second));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public async Task InvalidGramsReturnValidationProblem(string grams)
    {
        await Login();
        var response = await _client.GetAsync($"/Search/apple?grams={grams}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task SearchResultCanBePostedAsMealWithoutLosingCalories()
    {
        await Login();
        var search = await _client.GetFromJsonAsync<JsonElement>("/Search/apple");
        Assert.Equal(100, search.GetProperty("foods")[0].GetProperty("quantity").GetDouble());
        var response = await _client.PostAsJsonAsync("/Meals", search);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(70, created.GetProperty("meal").GetProperty("foods")[0].GetProperty("calories").GetInt32());
        var meals = await _client.GetFromJsonAsync<JsonElement>("/Meals");
        Assert.Equal(1, meals.GetArrayLength());
        Assert.Equal(70, meals[0].GetProperty("foods")[0].GetProperty("calories").GetInt32());
    }

    [Theory]
    [InlineData("{\"foods\":[]}")]
    [InlineData("{\"foods\":[null]}")]
    [InlineData("{\"foods\":[{\"name\":\"food\",\"quantity\":-1,\"macros\":{}}]}")]
    [InlineData("{\"foods\":[{\"name\":\"food\",\"quantity\":100,\"macros\":{\"fat\":-1}}]}")]
    [InlineData("{\"foods\":[{\"name\":\"food\",\"quantity\":100,\"macros\":null}]}")]
    public async Task InvalidMealsReturn400(string json)
    {
        await Login();
        var response = await _client.PostAsync("/Meals", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LegacyMealInputRemainsAccepted()
    {
        await Login();
        var response = await _client.PostAsJsonAsync("/Meals", new
        {
            foods = new[] { new { product_name = "Legacy food", brands = "", quantity = 100,
                nutriments = new Dictionary<string, double> { ["energy-kcal_value_computed"] = 70, ["proteins"] = 3 } } }
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(70, created.GetProperty("meal").GetProperty("foods")[0].GetProperty("calories").GetInt32());
    }

    [Fact]
    public async Task UnhandledErrorsDoNotExposeExceptionDetails()
    {
        await Login();
        var response = await _client.GetAsync("/Search/throw");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("private upstream details", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthAndEnvironmentGuardsWork()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/openapi/v1.json")).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}

using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.DTO.GtinSearch;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Core.Interface.Foods;
using Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using static Infrastructure.Services.Foods.Search.SearchModels;
using static Infrastructure.Services.Foods.Barcode.BarcodeModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests;

public sealed class ConsensusServiceTests
{
    [Fact]
    public async Task SearchPreservesScalingMetadataAndSourceAttributionAcrossCacheHits()
    {
        using var fixture = new Fixture();
        var service = fixture.Provider.GetRequiredService<IFreshFoodsService>();
        var first = (await service.FetchAsync("  APPLE  ", 50))!.Foods.Single();
        var second = (await service.FetchAsync("apple", 200))!.Foods.Single();
        Assert.Equal("Apple", first.Name);
        Assert.Equal(35, first.Calories);
        Assert.Equal(5, first.MacrosDto.Carbohydrates);
        Assert.Equal(140, second.Calories);
        Assert.Equal(20, second.MacrosDto.Carbohydrates);
        Assert.Equal("B", first.Grade);
        Assert.Equal("low", first.NutrientLevels!["fat"]);
        Assert.Equal(["OpenFoodFacts"], first.SourcesUsed);
        Assert.Equal(0.7, first.Confidence);
        Assert.Equal(1, fixture.SearchCalls);
        Assert.Equal(1, fixture.MetadataCalls);
        first.NutrientLevels["fat"] = "modified";
        var third = (await service.FetchAsync("apple"))!.Foods.Single();
        Assert.Equal("low", third.NutrientLevels!["fat"]);
    }

    [Fact]
    public async Task BarcodePreservesAnchorAndUsesServingIndependentCache()
    {
        using var fixture = new Fixture();
        var service = fixture.Provider.GetRequiredService<IBarcodeService>();
        var first = (await service.FetchAsync("4006381333931", 50))!.Foods.Single();
        var second = (await service.FetchAsync("4006381333931", 200))!.Foods.Single();
        Assert.Equal("Apple", first.Name);
        Assert.Equal("Orchard", first.Brands);
        Assert.Equal(35, first.Calories);
        Assert.Equal(140, second.Calories);
        Assert.Equal("B", first.Grade);
        Assert.Equal("low", first.NutrientLevels!["fat"]);
        Assert.Equal(["OpenFoodFactsBarcode", "OpenFoodFactsSearch"], first.SourcesUsed);
        Assert.Equal(2, fixture.BarcodeCalls);
        Assert.Equal(0, fixture.Gtin.SearchCalls);
    }

    [Theory]
    [InlineData("4006381333932")]
    [InlineData("123")]
    [InlineData("")]
    public async Task InvalidBarcodeDoesNotCallSources(string input)
    {
        using var fixture = new Fixture();
        Assert.Empty((await fixture.Provider.GetRequiredService<IBarcodeService>().FetchAsync(input))!.Foods);
        Assert.Equal(0, fixture.BarcodeCalls);
        Assert.Equal(0, fixture.Gtin.BarcodeCalls);
    }

    [Fact]
    public async Task ShortQueryDoesNotCallSources()
    {
        using var fixture = new Fixture();
        Assert.Empty((await fixture.Provider.GetRequiredService<IFreshFoodsService>().FetchAsync("a"))!.Foods);
        Assert.Equal(0, fixture.SearchCalls);
    }

    [Fact]
    public async Task ConcurrentSearchRequestsShareOneSourceFetch()
    {
        using var fixture = new Fixture();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.SearchGate = gate.Task;
        var service = fixture.Provider.GetRequiredService<IFreshFoodsService>();
        var requests = Enumerable.Range(0, 20).Select(_ => service.FetchAsync("apple")).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(requests);
        Assert.All(responses, response => Assert.Single(response!.Foods));
        Assert.Equal(1, fixture.SearchCalls);
    }

    [Fact]
    public async Task SourceFailureReturnsEmptyResultsInsteadOfThrowing()
    {
        using var fixture = new Fixture();
        fixture.FailSearch = true;
        var response = await fixture.Provider.GetRequiredService<IFreshFoodsService>().FetchAsync("apple");
        Assert.Empty(response!.Foods);
        Assert.Equal(1, fixture.Gtin.SearchCalls);
    }

    [Fact]
    public async Task EmptyBackgroundRefreshPreservesPreviousSearchResults()
    {
        using var fixture = new Fixture();
        var service = fixture.Provider.GetRequiredService<IFreshFoodsService>();
        await service.FetchAsync("apple");
        var cache = fixture.Provider.GetRequiredService<IMemoryCache>();
        var existing = cache.Get<FoodCacheEntry>("fresh_consensus_apple")!;
        cache.Set("fresh_consensus_apple", existing with { RefreshedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-60) });
        fixture.EmptySearch = true;

        var response = await service.FetchAsync("apple");
        Assert.Equal("Apple", response!.Foods.Single().Name);
        var preserved = cache.Get<FoodCacheEntry>("fresh_consensus_apple")!;
        Assert.Equal(existing.Foods, preserved.Foods);
        Assert.True(preserved.RefreshedAtUtc > existing.RefreshedAtUtc);
        Assert.Equal(2, fixture.SearchCalls);
    }

    [Fact]
    public async Task MissingAnchorDuringBackgroundRefreshPreservesBarcodeResult()
    {
        using var fixture = new Fixture();
        var service = fixture.Provider.GetRequiredService<IBarcodeService>();
        await service.FetchAsync("4006381333931");
        var cache = fixture.Provider.GetRequiredService<IMemoryCache>();
        var existing = cache.Get<BarcodeCacheEntry>("barcode_consensus_4006381333931")!;
        cache.Set("barcode_consensus_4006381333931", existing with { RefreshedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-60) });
        fixture.MissingBarcode = true;

        var response = await service.FetchAsync("4006381333931");
        Assert.Equal("Apple", response!.Foods.Single().Name);
        var preserved = cache.Get<BarcodeCacheEntry>("barcode_consensus_4006381333931")!;
        Assert.Equal(existing.Consensus, preserved.Consensus);
        Assert.True(preserved.RefreshedAtUtc > existing.RefreshedAtUtc);
    }

    [Fact]
    public async Task MetadataOutageDoesNotDiscardNutritionAndIsNegativelyCached()
    {
        using var fixture = new Fixture();
        fixture.FailMetadata = true;
        var service = fixture.Provider.GetRequiredService<IFreshFoodsService>();
        var first = (await service.FetchAsync("apple"))!.Foods.Single();
        var second = (await service.FetchAsync("apple"))!.Foods.Single();
        Assert.Equal(70, first.Calories);
        Assert.Equal(70, second.Calories);
        Assert.Null(first.NutrientLevels);
        Assert.Equal(2, fixture.MetadataCalls);
    }

    [Fact]
    public async Task EnoughPrimaryCandidatesSkipGtinFallback()
    {
        using var fixture = new Fixture();
        fixture.SearchProducts = Enumerable.Range(1, 4).Select(index => new OpenFoodSearchProduct
        {
            Name = $"Apple {index}", Brands = "Orchard", NutriScoreGrade = "b",
            Nutriments = new() { EnergyKcal100g = 70, Carbohydrates100g = 10, Fat100g = 2, Proteins100g = 3 }
        }).ToArray();
        var response = await fixture.Provider.GetRequiredService<IFreshFoodsService>().FetchAsync("apple");
        Assert.NotEmpty(response!.Foods);
        Assert.Equal(0, fixture.Gtin.SearchCalls);
    }

    [Fact]
    public async Task BarcodeRejectsNutritionFromDifferentProductIdentity()
    {
        using var fixture = new Fixture();
        fixture.SearchProducts = [new() { Name = "Pear", Brands = "Other brand",
            Nutriments = new() { EnergyKcal100g = 500, Carbohydrates100g = 50, Fat100g = 20, Proteins100g = 5 } }];
        var response = await fixture.Provider.GetRequiredService<IBarcodeService>().FetchAsync("4006381333931");
        var food = response!.Foods.Single();
        Assert.Equal(70, food.Calories);
        Assert.Equal(["OpenFoodFactsBarcode"], food.SourcesUsed);
    }

    internal sealed class Fixture : IDisposable
    {
        public ServiceProvider Provider { get; }
        public CountingGtin Gtin { get; } = new();
        public int SearchCalls;
        public int BarcodeCalls;
        public int MetadataCalls;
        public bool FailSearch;
        public bool EmptySearch;
        public bool MissingBarcode;
        public bool FailMetadata;
        public Task? SearchGate;
        public OpenFoodSearchProduct[] SearchProducts = [new()
        {
            Name = "Apple", Brands = "Orchard", NutriScoreGrade = "b",
            Nutriments = new() { EnergyKcal100g = 70, Carbohydrates100g = 10, Fat100g = 2, Proteins100g = 3 }
        }];

        public Fixture()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test",
                ["Jwt:Key"] = Convert.ToBase64String(new byte[64]), ["Jwt:TokenValidityMins"] = "60",
                ["Sources:Global:FailureThreshold"] = "1000",
                ["Sources:USDA:Enabled"] = "true", ["Sources:OpenFoodFacts:Enabled"] = "true",
                ["Sources:OpenFoodFactsSearch:Enabled"] = "true", ["Sources:GTINSearch:Enabled"] = "true"
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);
            services.AddSingleton<IClient<FreshFoodResponse>>(new StubClient<FreshFoodResponse>(_ =>
                Task.FromResult<FreshFoodResponse?>(new() { Foods = [] })));
            services.AddSingleton<IClient<OpenFoodSearchResponse>>(new StubClient<OpenFoodSearchResponse>(async _ =>
            {
                Interlocked.Increment(ref SearchCalls);
                if (SearchGate is not null) await SearchGate;
                if (FailSearch) throw new HttpRequestException("test outage");
                return new OpenFoodSearchResponse
                {
                    Products = EmptySearch ? [] : SearchProducts
                };
            }));
            services.AddSingleton<IClient<BarcodeResponse>>(new StubClient<BarcodeResponse>(_ =>
            {
                Interlocked.Increment(ref BarcodeCalls);
                if (MissingBarcode) return Task.FromResult<BarcodeResponse?>(new() { Product = null });
                return Task.FromResult<BarcodeResponse?>(new()
                {
                    Product = new() { Name = "Apple", Brands = "Orchard", NutriScoreGrade = "b",
                        NutrientLevels = new() { ["fat"] = "low" },
                        Nutriments = new() { EnergyKcal100g = 70, Carbohydrates100g = 10, Fat100g = 2, Proteins100g = 3 } }
                });
            }));
            services.AddSingleton<IClient<OpenFoodSearchALiciousResponse>>(new StubClient<OpenFoodSearchALiciousResponse>(_ =>
            {
                Interlocked.Increment(ref MetadataCalls);
                if (FailMetadata) throw new HttpRequestException("test metadata outage");
                return Task.FromResult<OpenFoodSearchALiciousResponse?>(new()
                {
                    Hits = [new() { ProductName = "Apple", NutriScoreGrade = "b",
                        NutrientLevels = new() { ["fat"] = "low" } }]
                });
            }));
            services.AddSingleton<IGtinSearchClient>(Gtin);
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public void Dispose() => Provider.Dispose();
    }

    private sealed class StubClient<T>(Func<string, Task<T?>> fetch) : IClient<T> where T : IResponse
    {
        public Task<T?> Fetch(string input, CancellationToken cancellationToken = default) => fetch(input);
    }

    internal sealed class CountingGtin : IGtinSearchClient
    {
        public int SearchCalls;
        public int BarcodeCalls;
        public Task<GtinSearchItem[]> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref SearchCalls);
            return Task.FromResult<GtinSearchItem[]>([]);
        }
        public Task<GtinSearchItem[]> LookupBarcodeAsync(string query, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref BarcodeCalls);
            return Task.FromResult<GtinSearchItem[]>([]);
        }
    }
}

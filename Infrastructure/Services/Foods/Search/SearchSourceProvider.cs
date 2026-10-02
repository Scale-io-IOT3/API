using System.Diagnostics;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Core.Interface.Foods;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Infrastructure.Services.Foods.Shared;
using static Infrastructure.Services.Foods.Search.SearchModels;
using static Infrastructure.Services.Foods.Search.SearchPolicy;
using static Infrastructure.Services.Foods.Search.SearchCandidateMapper;
using static Infrastructure.Validators.Foods.SearchCandidateValidator;
using static Infrastructure.Services.Foods.Shared.FoodText;
using static Infrastructure.Services.Foods.Shared.NutrientConsensus;

namespace Infrastructure.Services.Foods.Search;

internal sealed class SearchSourceProvider(
    IClient<FreshFoodResponse> usdaClient,
    IClient<OpenFoodSearchResponse> openFoodClient,
    IGtinSearchClient gtinSearchClient,
    IConfiguration configuration,
    ILogger<SearchSourceProvider> logger
)
{
    private readonly SourceCallPolicy _usda = SourceCallPolicy.From(configuration, Usda, true, 2200);
    private readonly SourceCallPolicy _openFood = SourceCallPolicy.From(configuration, OpenFoodFacts, false, 1500);
    private readonly SourceCallPolicy _gtin = SourceCallPolicy.From(configuration, GtinSearch, true, 4500);

    internal Task<SourceCandidates> FetchUsda(string query) =>
        Run(token => FetchUsdaCore(query, token), _usda, query, latency => new SourceCandidates([], latency), "consensus search");

    internal Task<SourceCandidates> FetchOpenFood(string query) =>
        Run(token => FetchOpenFoodCore(query, token), _openFood, query, latency => new SourceCandidates([], latency), "consensus search");

    internal Task<SourceCandidates> FetchGtinSearch(string query) =>
        Run(token => FetchGtinSearchCore(query, token), _gtin, query, latency => new SourceCandidates([], latency), "consensus search");

    private Task<T> Run<T>(
        Func<CancellationToken, Task<T>> call, SourceCallPolicy policy, string query,
        Func<int, T> empty, string operation) =>
        SourceCallExecutor.ExecuteWithBudget(call, policy.Settings, policy.TimeoutMs, query, logger,
            operation, empty, () => empty(0));

    private async Task<SourceCandidates> FetchUsdaCore(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await usdaClient.Fetch(normalizedQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {Usda} returned no response payload.");
        }

        var foods = response.FilterAndRank(normalizedQuery, MaxSourceCandidates).Foods;
        var candidates = foods
            .Select(FoodDto.FromFreshFood)
            .Select(dto => FromDto(Usda, dto, normalizedQuery, UsdaReliability))
            .Where(IsValid)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    private async Task<SourceCandidates> FetchOpenFoodCore(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await openFoodClient.Fetch(normalizedQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {OpenFoodFacts} returned no response payload.");
        }

        var candidates = (response.Products ?? [])
            .Select(product => FromOpenFood(product, normalizedQuery))
            .Where(candidate => candidate is not null)
            .Take(MaxSourceCandidates)
            .Cast<Candidate>()
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    private async Task<SourceCandidates> FetchGtinSearchCore(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await gtinSearchClient.SearchAsync(normalizedQuery, cancellationToken);
        sw.Stop();

        var candidates = (response ?? [])
            .Select(item => FromGtinSearch(item, normalizedQuery))
            .Where(candidate => candidate is not null)
            .Take(MaxSourceCandidates)
            .Cast<Candidate>()
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }
}

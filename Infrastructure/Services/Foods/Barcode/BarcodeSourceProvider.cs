using System.Diagnostics;
using Core.DTO.Barcodes;
using Core.DTO.Foods;
using Core.DTO.FreshFoods;
using Core.DTO.OpenFoodFacts;
using Core.Interface;
using Core.Interface.Foods;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Infrastructure.Services.Foods.Shared;
using static Infrastructure.Services.Foods.Barcode.BarcodeModels;
using static Infrastructure.Services.Foods.Barcode.BarcodePolicy;
using static Infrastructure.Services.Foods.Barcode.BarcodeCandidateMapper;
using static Infrastructure.Validators.Foods.BarcodeCandidateValidator;
using static Infrastructure.Services.Foods.Shared.FoodText;
using static Infrastructure.Services.Foods.Shared.NutrientConsensus;

namespace Infrastructure.Services.Foods.Barcode;

/// <summary>Retrieves barcode anchors and supporting search candidates using source-specific time budgets.</summary>
internal sealed class BarcodeSourceProvider(
    IClient<BarcodeResponse> barcodeClient,
    IClient<FreshFoodResponse> usdaClient,
    IClient<OpenFoodSearchResponse> openFoodSearchClient,
    IGtinSearchClient gtinSearchClient,
    IConfiguration configuration,
    ILogger<BarcodeSourceProvider> logger
)
{
    private readonly SourceCallPolicy _barcode = SourceCallPolicy.From(configuration, BarcodeSource, false, 1200, "OpenFoodFacts");
    private readonly SourceCallPolicy _gtinBarcode = SourceCallPolicy.From(configuration, GtinBarcodeSource, true, 1800, "GTINSearch");
    private readonly SourceCallPolicy _usda = SourceCallPolicy.From(configuration, UsdaSource, true, 2200);
    private readonly SourceCallPolicy _openFood = SourceCallPolicy.From(configuration, OpenFoodSearchSource, false, 1500, "OpenFoodFacts");
    private readonly SourceCallPolicy _gtinSearch = SourceCallPolicy.From(configuration, GtinSearchSource, true, 4500, "GTINSearch");

    internal Task<BarcodeAnchorResult> FetchBarcodeAnchor(string barcode) =>
        Run(token => FetchBarcodeAnchorCore(barcode, token), _barcode, barcode, latency => new BarcodeAnchorResult(null, latency), "barcode identity step");

    internal Task<RawSourceCandidates> FetchGtinBarcode(string barcode) =>
        Run(token => FetchGtinBarcodeCore(barcode, token), _gtinBarcode, barcode, latency => new RawSourceCandidates([], latency), "barcode identity step");

    internal Task<SourceCandidates> FetchUsda(string query, Candidate anchor) =>
        Run(token => FetchUsdaCore(query, anchor, token), _usda, query, latency => new SourceCandidates([], latency), "barcode consensus");

    internal Task<SourceCandidates> FetchOpenFoodSearch(string query, Candidate anchor) =>
        Run(token => FetchOpenFoodSearchCore(query, anchor, token), _openFood, query, latency => new SourceCandidates([], latency), "barcode consensus");

    internal Task<SourceCandidates> FetchGtinSearch(string query, Candidate anchor) =>
        Run(token => FetchGtinSearchCore(query, anchor, token), _gtinSearch, query, latency => new SourceCandidates([], latency), "barcode consensus");

    private Task<T> Run<T>(
        Func<CancellationToken, Task<T>> call, SourceCallPolicy policy, string query,
        Func<int, T> empty, string operation) =>
        SourceCallExecutor.ExecuteWithBudget(call, policy.Settings, policy.TimeoutMs, query, logger,
            operation, empty, () => empty(0));

    private async Task<BarcodeAnchorResult> FetchBarcodeAnchorCore(string barcode, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await barcodeClient.Fetch(barcode, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {BarcodeSource} returned no response payload.");
        }

        var product = response.Product;
        if (product is null || string.IsNullOrWhiteSpace(product.ResolvedName))
        {
            return new BarcodeAnchorResult(null, sw.ElapsedMilliseconds);
        }

        var macros = product.ResolvedMacros;
        var calories = ResolveCalories(macros.Calories, macros.Carbohydrates, macros.Fat, macros.Proteins);
        var raw = new RawCandidate(
            BarcodeSource,
            product.ResolvedName,
            product.ResolvedBrand,
            calories,
            macros.Carbohydrates,
            macros.Fat,
            macros.Proteins,
            BarcodeReliability,
            1.0,
            HasNutrition(macros.Calories, macros.Carbohydrates, macros.Fat, macros.Proteins),
            product.ResolvedNutritionGrade
        );

        return new BarcodeAnchorResult(raw, sw.ElapsedMilliseconds);
    }

    private async Task<RawSourceCandidates> FetchGtinBarcodeCore(string barcode, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var items = await gtinSearchClient.LookupBarcodeAsync(barcode, cancellationToken);
        sw.Stop();

        var raws = items
            .Select(item => ToRawFromGtin(item, GtinBarcodeSource, 1.0, GtinBarcodeReliability))
            .Where(raw => raw is not null)
            .Take(MaxSourceCandidates)
            .Cast<RawCandidate>()
            .ToList();

        return new RawSourceCandidates(raws, sw.ElapsedMilliseconds);
    }

    private async Task<SourceCandidates> FetchUsdaCore(string identityQuery, Candidate anchor, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await usdaClient.Fetch(identityQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {UsdaSource} returned no response payload.");
        }

        var foods = response.FilterAndRank(identityQuery, MaxSourceCandidates).Foods;
        var candidates = foods
            .Select(FoodDto.FromFreshFood)
            .Select(dto => FinalizeRaw(ToRawFromDto(UsdaSource, dto, identityQuery, UsdaReliability), anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    private async Task<SourceCandidates> FetchOpenFoodSearchCore(
        string identityQuery,
        Candidate anchor,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var response = await openFoodSearchClient.Fetch(identityQuery, cancellationToken);
        sw.Stop();

        if (response is null)
        {
            throw new HttpRequestException($"Source {OpenFoodSearchSource} returned no response payload.");
        }

        var candidates = (response.Products ?? [])
            .Select(ToRawFromOpenFoodSearch)
            .Where(raw => raw is not null)
            .Take(MaxSourceCandidates)
            .Cast<RawCandidate>()
            .Select(raw => FinalizeRaw(raw, anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }

    private async Task<SourceCandidates> FetchGtinSearchCore(
        string identityQuery,
        Candidate anchor,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var items = await gtinSearchClient.SearchAsync(identityQuery, cancellationToken);
        sw.Stop();

        var candidates = items
            .Select(item => ToRawFromGtin(item, GtinSearchSource, MatchQuality(identityQuery, Normalize(item.Name ?? string.Empty)), GtinSearchReliability))
            .Where(raw => raw is not null)
            .Take(MaxSourceCandidates)
            .Cast<RawCandidate>()
            .Select(raw => FinalizeRaw(raw, anchor))
            .Where(candidate => IsPlausible(candidate) && candidate.IdentitySimilarity >= MinIdentitySimilarity)
            .ToList();

        return new SourceCandidates(candidates, sw.ElapsedMilliseconds);
    }
}

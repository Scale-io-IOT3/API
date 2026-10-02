using Infrastructure.Services.Foods.Shared;
using Infrastructure.Validators.Foods;
using Xunit;
using BarcodeCandidate = Infrastructure.Services.Foods.Barcode.BarcodeModels.Candidate;
using SearchCandidate = Infrastructure.Services.Foods.Search.SearchModels.Candidate;

namespace Tests;

public sealed class FoodRulesTests
{
    [Theory]
    [InlineData("  Cr\u00e8me BR\u00dbL\u00c9E! ", "creme brulee")]
    [InlineData("apple--juice", "apple juice")]
    [InlineData("", "")]
    public void NormalizationPreservesExistingMatchingRules(string input, string expected)
    {
        Assert.Equal(expected, FoodText.Normalize(input));
    }

    [Theory]
    [InlineData("4006381333931", true)]
    [InlineData("4 006381 333931", true)]
    [InlineData("4006381333932", false)]
    [InlineData("123", false)]
    public void BarcodeValidationChecksNormalizedLengthAndCheckDigit(string input, bool valid)
    {
        Assert.Equal(valid, BarcodeInputValidator.TryNormalize(input, out _));
    }

    [Fact]
    public void SearchNeedsNutritionButBarcodeCanKeepAnIdentityOnlyAnchor()
    {
        var search = new SearchCandidate("source", "Apple", "", "apple", "", 0, 0, 0, 0, 1, 1, null);
        var barcode = new BarcodeCandidate("source", "Apple", "", "apple", "", 0, 0, 0, 0, 1, 1, 1, false, null);
        Assert.False(SearchCandidateValidator.IsValid(search));
        Assert.True(BarcodeCandidateValidator.IsPlausible(barcode));
        Assert.False(SearchCandidateValidator.IsValid(search with { Calories = 1300 }));
        Assert.False(BarcodeCandidateValidator.IsPlausible(barcode with { HasNutrition = true, Fat = 121 }));
    }

    [Fact]
    public void AggregationRetainsWeightedMedianAndOutlierRules()
    {
        var result = NutrientConsensus.Aggregate([(1, 1), (2, 1), (3, 1), (4, 1), (100, 1)]);
        Assert.Equal(2, result.Value);
        Assert.Equal(0.5, result.NormalizedMad);
        var weighted = NutrientConsensus.Aggregate([(10, 1), (20, 5), (30, 1)]);
        Assert.Equal(20, weighted.Value);
    }
}

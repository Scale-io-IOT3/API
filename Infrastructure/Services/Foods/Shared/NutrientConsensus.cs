

namespace Infrastructure.Services.Foods.Shared;

/// <summary>Aggregates per-100-gram nutrients using median-absolute-deviation outlier rejection and weighted medians.</summary>
internal static class NutrientConsensus
{
    /// <summary>Rejects robust statistical outliers before calculating a weighted nutrient median.</summary>
    /// <param name="values">Per-100-gram values with their source weights.</param>
    /// <returns>The aggregate and normalized dispersion; empty input returns zero with maximum dispersion.</returns>
    internal static AggregateResult Aggregate(List<(double Value, double Weight)> values)
    {
        if (values.Count == 0)
        {
            return new AggregateResult(0, 1);
        }

        // Reject outliers using median absolute deviation before taking the weighted median.
        var rawValues = values.Select(item => item.Value).ToList();
        var median = Median(rawValues);
        var deviations = rawValues.Select(value => Math.Abs(value - median)).ToList();
        var mad = Median(deviations);

        var filtered = mad <= 0
            ? values
            : [.. values.Where(item =>
            {
                var robustZ = 0.6745 * (item.Value - median) / mad;
                return Math.Abs(robustZ) <= 3.5;
            })];

        if (filtered.Count == 0)
        {
            filtered = values;
        }

        var consensus = WeightedMedian(filtered);
        var normalizedMad = consensus <= 0 ? Math.Min(1, mad) : Math.Min(1, mad / consensus);
        return new AggregateResult(consensus, normalizedMad);
    }

    private static double WeightedMedian(List<(double Value, double Weight)> values)
    {
        var ordered = values.OrderBy(item => item.Value).ToList();
        var totalWeight = ordered.Sum(item => item.Weight);
        var threshold = totalWeight / 2.0;
        var cumulative = 0.0;

        foreach (var (Value, Weight) in ordered)
        {
            cumulative += Weight;
            if (cumulative >= threshold)
            {
                return Value;
            }
        }

        return ordered[^1].Value;
    }

    private static double Median(List<double> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        var mid = ordered.Count / 2;

        return ordered.Count % 2 == 0
            ? (ordered[mid - 1] + ordered[mid]) / 2.0
            : ordered[mid];
    }

    internal static double ResolveCalories(double calories, double carbs, double fat, double proteins)
    {
        if (calories > 0)
        {
            return calories;
        }

        var computed = carbs * 4 + fat * 9 + proteins * 4;
        return computed > 0 ? computed : 0;
    }

    internal static bool HasNutrition(double calories, double carbs, double fat, double proteins)
    {
        return calories > 0 || carbs > 0 || fat > 0 || proteins > 0;
    }

    internal static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    internal sealed record AggregateResult(double Value, double NormalizedMad);
}

using System.Collections.ObjectModel;

namespace FantasyBasketball.Domain.Backtest;

public sealed record DecileCalibration(
    int Decile,
    int Count,
    decimal MeanProjected,
    decimal MeanActual,
    decimal Deviation);

public sealed record AccuracyReport(
    int Count,
    decimal Mae,
    decimal Rmse,
    decimal SpearmanRho,
    decimal TopKHitRate,
    IReadOnlyList<DecileCalibration> Deciles);

/// <summary>Pure projection accuracy: error, rank correlation, top-K overlap, decile calibration.</summary>
public static class AccuracyMetrics
{
    public static AccuracyReport Compute(
        IReadOnlyList<decimal> projected,
        IReadOnlyList<decimal> actual,
        int topK = 100)
    {
        ArgumentNullException.ThrowIfNull(projected);
        ArgumentNullException.ThrowIfNull(actual);
        if (projected.Count != actual.Count)
        {
            throw new ArgumentException("Projected and actual must have the same length.", nameof(actual));
        }

        if (projected.Count < 2)
        {
            throw new ArgumentException("At least two observations are required.", nameof(projected));
        }

        if (topK < 1)
        {
            throw new ArgumentException("Top-K must be at least one.", nameof(topK));
        }

        var count = projected.Count;
        var (absSum, squaredSum) = ErrorSums(projected, actual);
        var mae = absSum / count;
        var rmse = Sqrt(squaredSum / count);
        var rho = Spearman(projected, actual);
        var hitRate = TopKHitRate(projected, actual, Math.Min(topK, count));
        var deciles = ComputeDeciles(projected, actual);

        return new AccuracyReport(
            count, mae, rmse, rho, hitRate, new ReadOnlyCollection<DecileCalibration>(deciles));
    }

    private static (decimal AbsSum, decimal SquaredSum) ErrorSums(
        IReadOnlyList<decimal> projected,
        IReadOnlyList<decimal> actual)
    {
        var absSum = 0m;
        var squaredSum = 0m;
        for (var i = 0; i < projected.Count; i++)
        {
            var error = projected[i] - actual[i];
            absSum += Math.Abs(error);
            squaredSum += error * error;
        }

        return (absSum, squaredSum);
    }

    private static decimal TopKHitRate(
        IReadOnlyList<decimal> projected,
        IReadOnlyList<decimal> actual,
        int k)
    {
        var projectedTop = TopIndexes(projected, k);
        var actualTop = TopIndexes(actual, k);
        var hits = 0;
        foreach (var index in projectedTop)
        {
            if (actualTop.Contains(index))
            {
                hits++;
            }
        }

        return (decimal)hits / k;
    }

    private static List<DecileCalibration> ComputeDeciles(
        IReadOnlyList<decimal> projected,
        IReadOnlyList<decimal> actual)
    {
        var count = projected.Count;
        var ordered = Enumerable.Range(0, count)
            .OrderBy(i => projected[i])
            .ThenBy(i => i)
            .ToArray();
        var deciles = new List<DecileCalibration>();
        for (var d = 0; d < 10; d++)
        {
            var from = d * count / 10;
            var to = (d + 1) * count / 10;
            if (from >= to)
            {
                continue;
            }

            var projectedSum = 0m;
            var actualSum = 0m;
            for (var slot = from; slot < to; slot++)
            {
                projectedSum += projected[ordered[slot]];
                actualSum += actual[ordered[slot]];
            }

            var groupCount = to - from;
            var meanProjected = projectedSum / groupCount;
            var meanActual = actualSum / groupCount;
            deciles.Add(new DecileCalibration(
                d + 1, groupCount, meanProjected, meanActual, meanActual - meanProjected));
        }

        return deciles;
    }

    private static HashSet<int> TopIndexes(IReadOnlyList<decimal> values, int k) =>
        Enumerable.Range(0, values.Count)
            .OrderByDescending(i => values[i])
            .ThenBy(i => i)
            .Take(k)
            .ToHashSet();

    private static decimal Spearman(IReadOnlyList<decimal> projected, IReadOnlyList<decimal> actual)
    {
        var projectedRanks = AverageRanks(projected);
        var actualRanks = AverageRanks(actual);
        var n = projectedRanks.Length;
        var projectedMean = projectedRanks.Average();
        var actualMean = actualRanks.Average();
        var covariance = 0m;
        var projectedVariance = 0m;
        var actualVariance = 0m;
        for (var i = 0; i < n; i++)
        {
            var projectedDeviation = projectedRanks[i] - projectedMean;
            var actualDeviation = actualRanks[i] - actualMean;
            covariance += projectedDeviation * actualDeviation;
            projectedVariance += projectedDeviation * projectedDeviation;
            actualVariance += actualDeviation * actualDeviation;
        }

        if (projectedVariance == 0m || actualVariance == 0m)
        {
            return 0m;
        }

        return covariance / Sqrt(projectedVariance * actualVariance);
    }

    private static decimal[] AverageRanks(IReadOnlyList<decimal> values)
    {
        var n = values.Count;
        var order = Enumerable.Range(0, n).OrderBy(i => values[i]).ThenBy(i => i).ToArray();
        var ranks = new decimal[n];
        var slot = 0;
        while (slot < n)
        {
            var end = slot;
            while (end + 1 < n && values[order[end + 1]] == values[order[slot]])
            {
                end++;
            }

            var average = (decimal)(slot + 1 + end + 1) / 2m;
            for (var s = slot; s <= end; s++)
            {
                ranks[order[s]] = average;
            }

            slot = end + 1;
        }

        return ranks;
    }

    private static decimal Sqrt(decimal value) => (decimal)Math.Sqrt((double)value);
}

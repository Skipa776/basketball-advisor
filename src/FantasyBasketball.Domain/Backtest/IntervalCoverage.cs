namespace FantasyBasketball.Domain.Backtest;

/// <summary>How often central 80% intervals (mean ± 1.2816 SD) held the actual value.</summary>
public sealed record IntervalCoverage(int Count, decimal Coverage80, decimal MeanSd)
{
    public const decimal Z80 = 1.2815515655446004m;

    public static IntervalCoverage Compute(IReadOnlyList<decimal> projected, IReadOnlyList<decimal> sds, IReadOnlyList<decimal> actual)
    {
        ArgumentNullException.ThrowIfNull(projected);
        ArgumentNullException.ThrowIfNull(sds);
        ArgumentNullException.ThrowIfNull(actual);
        if (projected.Count != actual.Count || sds.Count != actual.Count || actual.Count == 0)
        {
            throw new ArgumentException("Projected, SD and actual series must be the same non-zero length.", nameof(actual));
        }

        var inside = actual.Where((value, index) => Math.Abs(value - projected[index]) <= Z80 * sds[index]).Count();
        return new IntervalCoverage(actual.Count, (decimal)inside / actual.Count, sds.Average());
    }
}

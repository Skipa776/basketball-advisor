namespace FantasyBasketball.Domain.Statistics;

/// <summary>
/// Games played out of <see cref="Trials"/>: a Binomial whose success rate is Beta(alpha, beta).
/// The pmf is built once, so quantiles and draws are table lookups.
/// </summary>
public sealed class BetaBinomial
{
    private readonly decimal[] cumulative;

    public BetaBinomial(int trials, decimal alpha, decimal beta)
    {
        if (trials < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trials));
        }

        if (alpha <= 0m || beta <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha and beta must be positive.");
        }

        Trials = trials;
        Alpha = alpha;
        Beta = beta;
        // log pmf(k) = log C(n, k) + log B(k + a, n - k + b) - log B(a, b).
        var a = alpha;
        var b = beta;
        var logBeta = LogGamma.Of(a) + LogGamma.Of(b) - LogGamma.Of(a + b);
        var pmf = Enumerable.Range(0, trials + 1)
            .Select(k => Math.Exp((double)(
                LogGamma.Of(trials + 1) - LogGamma.Of(k + 1) - LogGamma.Of(trials - k + 1)
                + LogGamma.Of(k + a) + LogGamma.Of(trials - k + b) - LogGamma.Of(trials + a + b)
                - logBeta)))
            .ToArray();
        var total = pmf.Sum();
        var running = 0d;  // the pmf stays in binary floating point; only the cumulative table is decimal
        cumulative = pmf.Select(p => (decimal)(running += p / total)).ToArray();
    }

    public int Trials { get; }

    public decimal Alpha { get; }

    public decimal Beta { get; }

    public decimal Mean => Trials * Alpha / (Alpha + Beta);

    public decimal Variance =>
        Trials * Alpha * Beta * (Alpha + Beta + Trials) / ((Alpha + Beta) * (Alpha + Beta) * (Alpha + Beta + 1m));

    /// <summary>The smallest k with P(X ≤ k) ≥ p; also the inverse-CDF draw for a uniform p.</summary>
    public int Quantile(decimal p)
    {
        if (p is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(p));
        }

        var index = Array.BinarySearch(cumulative, p);
        return Math.Min(index >= 0 ? index : ~index, Trials);
    }
}

/// <summary>
/// Lanczos approximation (g = 7, n = 9), relative error about 1e-15 for x &gt; 0. Decimal in and
/// out like <see cref="NormalDistribution.Cdf"/>; the series runs in binary floating point.
/// </summary>
public static class LogGamma
{
    private static readonly double[] Coefficients =
    [
        0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313,
        -176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6,
        1.5056327351493116e-7,
    ];

    public static decimal Of(decimal value)
    {
        var x = (double)value;
        if (x < 0.5)
        {
            // Reflection: Gamma(x) Gamma(1 - x) = pi / sin(pi x).
            return (decimal)Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - Of(1m - value);
        }

        x -= 1;
        var sum = Coefficients[0];
        for (var i = 1; i < Coefficients.Length; i++)
        {
            sum += Coefficients[i] / (x + i);
        }

        var t = x + 7.5;
        return (decimal)((0.5 * Math.Log(2 * Math.PI)) + ((x + 0.5) * Math.Log(t)) - t + Math.Log(sum));
    }
}

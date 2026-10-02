namespace FantasyBasketball.Domain.Statistics;

public static class NormalDistribution
{
    /// <summary>
    /// Standard normal CDF via the Abramowitz-Stegun 7.1.26 erf approximation
    /// (absolute error &lt; 1.5e-7), shared by draft survival odds and heat labels.
    /// </summary>
    public static decimal Cdf(decimal z)
    {
        var x = (double)z / Math.Sqrt(2d);
        var sign = x < 0d ? -1d : 1d;
        var value = Math.Abs(x);
        var t = 1d / (1d + (0.3275911d * value));
        var polynomial = t * (0.254829592d
            + t * (-0.284496736d
            + t * (1.421413741d
            + t * (-1.453152027d
            + t * 1.061405429d))));
        var erf = sign * (1d - (polynomial * Math.Exp(-value * value)));
        return (decimal)((erf / 2d) + 0.5d);
    }
}

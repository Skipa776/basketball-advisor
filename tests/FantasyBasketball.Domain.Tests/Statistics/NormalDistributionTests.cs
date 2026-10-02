using FantasyBasketball.Domain.Statistics;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Statistics;

public sealed class NormalDistributionTests
{
    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(1, 0.8413447461)]
    [InlineData(-1.96, 0.0249978951)]
    [InlineData(2.5, 0.9937903347)]
    public void ND01_cdf_matches_published_values_within_the_approximation_error(decimal z, decimal expected) =>
        Math.Abs(NormalDistribution.Cdf(z) - expected).ShouldBeLessThan(0.0000002m);
}

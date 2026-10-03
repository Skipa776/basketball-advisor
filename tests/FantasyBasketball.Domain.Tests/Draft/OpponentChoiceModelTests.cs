using FantasyBasketball.Domain.Draft;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Draft;

public sealed class OpponentChoiceModelTests
{
    private static readonly OpponentChoiceModel Model = new(new OpponentChoiceParameters([1, 5], [1m, 2m], 1m, 60));

    [Fact]
    public void OC01_probabilities_are_the_softmax_of_market_and_need()
    {
        // Round 1: u = -log(1) = 0 and -log(e) + 1 = 0, so an even split.
        var even = Model.Probabilities(1, [1m, (decimal)Math.E], [false, true]);
        even[0].ShouldBe(0.5m, 0.0000001m);
        even[1].ShouldBe(0.5m, 0.0000001m);

        // Round 5 uses lambda 2: u = -2 log(2) vs -2 log(4), a 4:1 ratio.
        var later = Model.Probabilities(5, [2m, 4m], [false, false]);
        later[0].ShouldBe(0.8m, 0.0000001m);
        later.Sum().ShouldBe(1m, 0.0000001m);
    }

    [Fact]
    public void OC02_choose_inverts_the_cdf_and_bad_inputs_throw()
    {
        Model.Choose(5, [2m, 4m], [false, false], 0.79m).ShouldBe(0);
        Model.Choose(5, [2m, 4m], [false, false], 0.81m).ShouldBe(1);
        Should.Throw<ArgumentException>(() => Model.Probabilities(1, [0m], [false]));
        Should.Throw<ArgumentException>(() => OpponentChoiceParameters.Parse("""{"roundGroups":[2],"lambdas":[1],"eta":0,"candidates":60}"""));
    }
}

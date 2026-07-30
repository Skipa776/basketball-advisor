using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

public sealed class BaselineProjector(
    MinutesProjector minutesProjector,
    ProjectionOptions options)
{
    public BaselineProjection Project(
        Guid baselineId,
        ObservedStats observed,
        StatLine leagueAverageRates)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(leagueAverageRates);
        if (!options.IsValid())
        {
            throw new ArgumentException("Projection options are invalid.", nameof(options));
        }

        var source = observed.Source;
        var playerMinutes = source.Totals[StatKey.MIN];
        var rateDenominator = playerMinutes + options.RateShrinkageMinutes;
        var rates = new StatLine(LeagueAverageRateCalculator.CountingStats()
            .ToDictionary(
                stat => stat,
                stat => (source.Totals[stat]
                        + (options.RateShrinkageMinutes * leagueAverageRates[stat]))
                    / rateDenominator));
        var projectedMinutes = minutesProjector.Project(
            source.GamesPlayed,
            source.MinutesPerGame,
            options);
        var projectedValues = LeagueAverageRateCalculator.CountingStats()
            .ToDictionary(
                stat => stat,
                stat => rates[stat] * projectedMinutes);
        projectedValues[StatKey.MIN] = projectedMinutes;
        var projectedGames = decimal.Round(
            (options.GamesPriorWeight * source.GamesPlayed)
            + ((1m - options.GamesPriorWeight)
                * options.Durability
                * options.SeasonGames),
            0,
            MidpointRounding.AwayFromZero);

        return new BaselineProjection(
            baselineId,
            observed.PlayerId,
            projectedMinutes,
            rates,
            new StatLine(projectedValues),
            decimal.ToInt32(projectedGames),
            observed.AsOf,
            options.ModelVersion);
    }
}

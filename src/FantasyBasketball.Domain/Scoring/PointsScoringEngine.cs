using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Scoring;

public sealed class PointsScoringEngine : IScoringEngine
{
    public LeagueType Handles => LeagueType.Points;

    public decimal Score(StatLine line, FantasyLeague league)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(league);

        if (league.Type != Handles)
        {
            throw new ArgumentException(
                "PointsScoringEngine requires a points league.",
                nameof(league));
        }

        var total = 0m;
        foreach (var rule in league.ScoringRules)
        {
            total += line[rule.Stat] * rule.PointsPerUnit;
        }

        return total;
    }

    public IReadOnlyDictionary<StatKey, decimal> ScoreCategories(
        StatLine line,
        FantasyLeague league) =>
        throw new NotSupportedException(
            "Points leagues produce a scalar score, not category totals.");
}

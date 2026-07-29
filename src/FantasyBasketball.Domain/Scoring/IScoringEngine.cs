using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Scoring;

public interface IScoringEngine
{
    LeagueType Handles { get; }

    decimal Score(StatLine line, FantasyLeague league);

    IReadOnlyDictionary<StatKey, decimal> ScoreCategories(
        StatLine line,
        FantasyLeague league);
}

using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Scoring;

public sealed class CategoryScoringEngine : IScoringEngine
{
    public LeagueType Handles => LeagueType.Categories;

    public decimal Score(StatLine line, FantasyLeague league) =>
        throw new NotSupportedException(
            "Category leagues produce category totals, not a scalar score.");

    public IReadOnlyDictionary<StatKey, decimal> ScoreCategories(
        StatLine line,
        FantasyLeague league)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(league);

        if (league.Type != Handles)
        {
            throw new ArgumentException(
                "CategoryScoringEngine requires a category league.",
                nameof(league));
        }

        return new ReadOnlyDictionary<StatKey, decimal>(
            league.Categories.ToDictionary(category => category, category => line[category]));
    }
}

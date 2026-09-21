using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;

namespace FantasyBasketball.Domain.Trends;

public sealed record PlayerHeatOptions
{
    public const string ModelVersion = "appearance-heat-v1";
    public int RecentGames { get; init; } = 3;
    public int MinimumBaselineGames { get; init; } = 10;
    public int MaximumBaselineGames { get; init; } = 30;

    public bool IsValid() => RecentGames > 0
        && MinimumBaselineGames > 0
        && MaximumBaselineGames >= MinimumBaselineGames;
}

public sealed record ScoredAppearance(
    Guid GameId,
    DateOnly PlayedOn,
    decimal FantasyPoints,
    DataProvenance Provenance);

public sealed record PlayerHeatResult(
    PlayerId PlayerId,
    Guid LeagueId,
    int SeasonEndYear,
    DateOnly ThroughDate,
    string ModelVersion,
    PlayerHeatOptions Policy,
    int SeasonAppearances,
    ReadOnlyCollection<ScoredAppearance> CurrentWindow,
    ReadOnlyCollection<ScoredAppearance> BaselineWindow,
    ReadOnlyCollection<ScoredAppearance> RecentWindow,
    decimal? CurrentAverage,
    decimal? BaselineAverage,
    decimal? RecentAverage,
    decimal? PointsAboveBaseline,
    decimal? RelativeLift,
    int? RecentGamesAboveBaseline)
{
    public DateOnly? LatestAppearance => CurrentWindow.LastOrDefault()?.PlayedOn;
    public bool HasComparison => PointsAboveBaseline.HasValue;
    public bool? IsAboveBaseline => PointsAboveBaseline is { } delta ? delta > 0m : null;
}

public sealed record PlayerPerformanceRankings(
    ReadOnlyCollection<PlayerHeatResult> BestPerforming,
    ReadOnlyCollection<PlayerHeatResult> Hottest);

/// <summary>Descriptive appearance windows; independent of opportunity TrendScore.</summary>
public sealed class PlayerHeatCalculator(PointsScoringEngine scoring, PlayerHeatOptions options)
{
    public PlayerHeatResult Calculate(
        PlayerId playerId,
        FantasyLeague league,
        int seasonEndYear,
        DateOnly throughDate,
        IReadOnlyList<PlayerGameSample> games)
    {
        Validate(league, seasonEndYear, games);
        if (playerId.Value == Guid.Empty)
        {
            throw new ArgumentException("Player ID is required.", nameof(playerId));
        }

        var history = games.Where(game => game.PlayerId == playerId
                && game.SeasonEndYear == seasonEndYear
                && game.PlayedOn <= throughDate)
            .ToArray();
        if (history.Select(game => game.GameId).Distinct().Count() != history.Length)
        {
            throw new ArgumentException("Duplicate game observations must be resolved before analysis.", nameof(games));
        }

        var appearances = history.Where(game => game.IsFinal && game.DidPlay)
            .OrderBy(game => game.PlayedOn)
            .ThenBy(game => game.GameId)
            .Select(game => new ScoredAppearance(
                game.GameId, game.PlayedOn, scoring.Score(game.Statistics!, league), game.Provenance))
            .ToArray();
        var recent = appearances.TakeLast(options.RecentGames).ToArray();
        var baseline = appearances.Take(Math.Max(0, appearances.Length - options.RecentGames))
            .TakeLast(options.MaximumBaselineGames).ToArray();
        var current = appearances.TakeLast(options.MaximumBaselineGames).ToArray();
        decimal? currentAverage = current.Length >= options.MinimumBaselineGames
            ? current.Average(game => game.FantasyPoints) : null;
        decimal? baselineAverage = baseline.Length >= options.MinimumBaselineGames
            ? baseline.Average(game => game.FantasyPoints) : null;
        decimal? recentAverage = recent.Length == options.RecentGames
            ? recent.Average(game => game.FantasyPoints) : null;
        var delta = recentAverage - baselineAverage;
        decimal? relative = baselineAverage is { } average && average != 0m
            ? delta / Math.Abs(average) : null;
        int? above = delta.HasValue
            ? recent.Count(game => game.FantasyPoints > baselineAverage!.Value) : null;
        return new PlayerHeatResult(
            playerId, league.Id, seasonEndYear, throughDate, PlayerHeatOptions.ModelVersion,
            options, appearances.Length, Array.AsReadOnly(current), Array.AsReadOnly(baseline),
            Array.AsReadOnly(recent), currentAverage, baselineAverage, recentAverage, delta, relative, above);
    }

    public PlayerPerformanceRankings Rank(
        FantasyLeague league,
        int seasonEndYear,
        DateOnly throughDate,
        IReadOnlyList<PlayerGameSample> games)
    {
        Validate(league, seasonEndYear, games);
        var results = games.Where(game => game.SeasonEndYear == seasonEndYear && game.PlayedOn <= throughDate)
            .GroupBy(game => game.PlayerId)
            .Select(group => Calculate(group.Key, league, seasonEndYear, throughDate, group.ToArray()))
            .ToArray();
        return new PlayerPerformanceRankings(
            Array.AsReadOnly(results.Where(result => result.CurrentAverage.HasValue)
                .OrderByDescending(result => result.CurrentAverage)
                .ThenBy(result => result.PlayerId.Value).ToArray()),
            Array.AsReadOnly(results.Where(result => result.PointsAboveBaseline > 0m)
                .OrderByDescending(result => result.PointsAboveBaseline)
                .ThenBy(result => result.PlayerId.Value).ToArray()));
    }

    private void Validate(FantasyLeague league, int seasonEndYear, IReadOnlyList<PlayerGameSample> games)
    {
        ArgumentNullException.ThrowIfNull(scoring);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(games);
        if (!options.IsValid())
        {
            throw new ArgumentException("Heat window options are invalid.", nameof(options));
        }

        if (league.Type != LeagueType.Points)
        {
            throw new ArgumentException("Player heat requires a points league.", nameof(league));
        }

        if (seasonEndYear < 1947)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonEndYear));
        }
    }
}

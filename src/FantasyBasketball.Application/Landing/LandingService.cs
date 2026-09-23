using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Landing;

public sealed record LandingLine(
    Guid PlayerId, string Name, decimal Minutes, decimal FantasyPoints, int CategoriesWon,
    IReadOnlyDictionary<string, decimal> Line);

public sealed record LandingDay(
    DateOnly? Date, string Source, string Scoring, int PoolSize, IReadOnlyList<LandingLine> Players);

public sealed record LandingRiser(
    Guid PlayerId, string Name, DateOnly LatestAppearance, int CategoriesWon, decimal RecentAverage,
    decimal BaselineAverage, int Streak, decimal PercentAboveBaseline, string Status);

public sealed record LandingRisers(
    DateOnly? ThroughDate, string Source, string Scoring, PlayerHeatOptions Policy,
    IReadOnlyList<LandingRiser> Players);

/// <summary>Waiver-status thresholds for the public risers table, owner-approved 2026-09-23.</summary>
public static class RiserStatus
{
    public const decimal MustAddLift = 0.40m;
    public const int MustAddStreak = 3;
    public const decimal AddLift = 0.20m;

    public static string For(decimal relativeLift, int streak) =>
        relativeLift >= MustAddLift && streak >= MustAddStreak ? "Must add"
        : relativeLift >= AddLift ? "Add"
        : relativeLift > 0m ? "Watch"
        : "Hold";
}

/// <summary>
/// Anonymous, read-only views of stored NBA reference data for the landing page,
/// scored under ESPN default points and the nine standard categories.
/// </summary>
public sealed class LandingService(
    IBoxScoreRepository boxScores,
    IPlayerRepository players,
    PlayerHeatCalculator calculator,
    PlayerHeatOptions policy)
{
    public const string Source = DataSourceName.BasketballReference;
    private static readonly FantasyLeague Scoring = LeagueCatalog.CreateEspnDefaultPointsLeague();
    private static readonly PointsScoringEngine Points = new();
    private static readonly StatKey[] ShownStats =
    [
        StatKey.MIN, StatKey.PTS, StatKey.REB, StatKey.AST, StatKey.STL, StatKey.BLK,
        StatKey.FG3M, StatKey.TOV, StatKey.FGM, StatKey.FGA, StatKey.FTM, StatKey.FTA,
    ];

    public async Task<LandingDay> DailyAsync(DateOnly? date, CancellationToken token)
    {
        var resolved = await ResolveDateAsync(date, token);
        if (resolved is null)
        {
            return new LandingDay(null, Source, Scoring.Name, 0, []);
        }

        var day = resolved.Value;
        var played = (await ListThroughAsync(day, token))
            .Where(sample => sample.PlayedOn == day && sample.DidPlay).ToArray();
        var pool = played.Select(sample => sample.Statistics!).ToArray();
        var lines = new List<LandingLine>();
        foreach (var (id, name) in await FeaturedIdsAsync(token))
        {
            var sample = played.FirstOrDefault(entry => entry.PlayerId == id);
            if (sample is null)
            {
                continue;
            }

            var line = sample.Statistics!;
            lines.Add(new LandingLine(id.Value, name, line[StatKey.MIN], Points.Score(line, Scoring),
                CategoriesWon.Count(line, pool),
                ShownStats.ToDictionary(stat => stat.ToString(), stat => line[stat])));
        }

        return new LandingDay(day, Source, Scoring.Name, pool.Length,
            lines.OrderByDescending(line => line.FantasyPoints).ToArray());
    }

    public async Task<LandingRisers> RisersAsync(DateOnly? throughDate, int limit, CancellationToken token)
    {
        if (limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 50.");
        }

        var resolved = await ResolveDateAsync(throughDate, token);
        if (resolved is null)
        {
            return new LandingRisers(null, Source, Scoring.Name, policy, []);
        }

        var through = resolved.Value;
        var samples = await ListThroughAsync(through, token);
        var featured = (await FeaturedIdsAsync(token)).Select(entry => entry.Id).ToHashSet();
        var season = SeasonEndYear(through);
        var ranked = samples.Where(sample => !featured.Contains(sample.PlayerId))
            .GroupBy(sample => sample.PlayerId)
            .Select(group => calculator.Calculate(group.Key, Scoring, season, through, group.ToArray()))
            .Where(result => result.PointsAboveBaseline > 0m && result.RelativeLift.HasValue)
            .OrderByDescending(result => result.PointsAboveBaseline)
            .ThenBy(result => result.PlayerId.Value)
            .Take(limit)
            .ToArray();
        var pools = samples.Where(sample => sample.DidPlay)
            .GroupBy(sample => sample.PlayedOn)
            .ToDictionary(group => group.Key, group => group.Select(sample => sample.Statistics!).ToArray());
        var risers = new List<LandingRiser>(ranked.Length);
        foreach (var result in ranked)
        {
            var latest = samples.Where(sample => sample.PlayerId == result.PlayerId && sample.DidPlay)
                .MaxBy(sample => sample.PlayedOn)!;
            var streak = result.CurrentWindow.Reverse()
                .TakeWhile(game => game.FantasyPoints > result.BaselineAverage!.Value).Count();
            var name = (await players.GetAsync(result.PlayerId, token))?.FullName ?? "Unknown player";
            risers.Add(new LandingRiser(result.PlayerId.Value, name, latest.PlayedOn,
                CategoriesWon.Count(latest.Statistics!, pools[latest.PlayedOn]),
                result.RecentAverage!.Value, result.BaselineAverage!.Value, streak,
                Math.Round(result.RelativeLift!.Value * 100m, 1),
                RiserStatus.For(result.RelativeLift.Value, streak)));
        }

        return new LandingRisers(through, Source, Scoring.Name, policy, risers);
    }

    private async Task<DateOnly?> ResolveDateAsync(DateOnly? requested, CancellationToken token)
    {
        if (requested is not null)
        {
            return requested;
        }

        var pools = await boxScores.ListPoolsAsync(token);
        return pools.Where(pool => pool.Source == Source)
            .Select(pool => (DateOnly?)pool.LatestGameDate)
            .Max();
    }

    private Task<IReadOnlyList<PlayerGameSample>> ListThroughAsync(DateOnly through, CancellationToken token) =>
        boxScores.ListAsync(SeasonEndYear(through), Source, NbaGamePhase.RegularSeason, through, token);

    // NBA seasons start in October; August onward belongs to the next season.
    private static int SeasonEndYear(DateOnly date) => date.Month >= 8 ? date.Year + 1 : date.Year;

    private async Task<IReadOnlyList<(PlayerId Id, string Name)>> FeaturedIdsAsync(CancellationToken token)
    {
        var featured = new List<(PlayerId, string)>();
        foreach (var name in FeaturedPlayers.Names)
        {
            foreach (var player in await players.FindByNormalizedNameAsync(PlayerName.Normalize(name), token))
            {
                featured.Add((player.Id, player.FullName));
            }
        }

        return featured;
    }
}

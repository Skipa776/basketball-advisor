using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Landing;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Streaming;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Leagues;

public sealed record StreamingMoveView(DateOnly Day, Guid AddId, string Add, Guid DropId, string Drop, decimal Gain,
    IReadOnlyList<DateOnly> AddUsableDays, IReadOnlyList<DateOnly> DropUsableDaysLost, int AcquisitionsUsed);

public sealed record StreamingPlanView(DateOnly From, DateOnly To, string Scoring, int AcquisitionLimit,
    decimal BaselineValue, decimal PlannedValue, int FreeAgentsConsidered, IReadOnlyList<StreamingMoveView> Moves,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Feeds the streaming planner with the user's roster and the league's free agents for the
/// rest of the week. Per-game value is the last-10 average under league scoring; rest-of-season
/// value is that average over the team's remaining scheduled games.
/// </summary>
public sealed class StreamingService(
    ILeagueRepository leagues, ILeagueTeamRepository teams, IPlayerRepository players,
    IGameRepository games, IBoxScoreRepository boxScores, LandingOptions options, TimeProvider clock)
{
    public const int RecentGames = 10;
    public const int FreeAgentsConsidered = 40;
    private static readonly TimeZoneInfo GameDateZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly PointsScoringEngine points = new();

    public async Task<StreamingPlanView> PlanAsync(Guid leagueId, DateOnly? day, CancellationToken token)
    {
        var league = await leagues.GetAsync(leagueId, token) ?? throw new ResourceNotFoundException("League was not found.");
        if (league.Type != LeagueType.Points)
        {
            throw new ResourceConflictException("Streaming uses points scoring; category leagues are not supported yet.");
        }

        var rosters = await teams.ListAsync(leagueId, token);
        var mine = rosters.SingleOrDefault(team => team.IsUsersTeam)
            ?? throw new ResourceConflictException("Import your league's rosters and mark your team (Mine = yes) first.");

        var today = day ?? options.AsOf ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), GameDateZone).DateTime);
        var weekEnd = today.AddDays(6 - (((int)today.DayOfWeek + 6) % 7));
        var horizon = Enumerable.Range(0, weekEnd.DayNumber - today.DayNumber + 1).Select(today.AddDays).ToArray();
        var season = today.Month >= 8 ? today.Year + 1 : today.Year;
        var seasonEnd = new DateOnly(season, 6, 30);
        var history = (await boxScores.ListAsync(season, DataSourceName.BasketballReference, NbaGamePhase.RegularSeason, today.AddDays(-1), token))
            .Where(sample => sample.DidPlay).GroupBy(sample => sample.PlayerId)
            .ToDictionary(group => group.Key, group => group.OrderBy(sample => sample.PlayedOn).TakeLast(RecentGames)
                .Average(sample => points.Score(sample.Statistics!, league)));
        var schedule = await games.ListScheduledAsync(DataSourceName.BallDontLie, StartOfDayUtc(today), StartOfDayUtc(seasonEnd), token);
        var gameDays = schedule.SelectMany(game => new[] { (Team: game.HomeTeamId, Game: game), (Team: game.AwayTeamId, Game: game) })
            .GroupBy(entry => entry.Team)
            .ToDictionary(group => group.Key, group => group.Select(entry => PlayedOn(entry.Game.StartsAt)).ToArray());

        async Task<StreamingPlayer?> Build(PlayerId id)
        {
            var player = await players.GetAsync(id, token);
            if (player is null)
            {
                return null;
            }

            var perGame = Math.Round(history.GetValueOrDefault(id), 2);
            var days = player.CurrentTeamId is { } team ? gameDays.GetValueOrDefault(team, []) : [];
            return new StreamingPlayer(id, player.FullName, player.Positions, perGame, perGame * days.Length,
                days.Where(date => date <= weekEnd).ToHashSet());
        }

        var roster = new List<StreamingPlayer>();
        foreach (var id in mine.Players)
        {
            if (await Build(id) is { } player)
            {
                roster.Add(player);
            }
        }

        var rostered = rosters.SelectMany(team => team.Players).ToHashSet();
        var freeAgents = new List<StreamingPlayer>();
        foreach (var id in history.Where(entry => !rostered.Contains(entry.Key))
                     .OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key.Value)
                     .Take(FreeAgentsConsidered).Select(entry => entry.Key))
        {
            if (await Build(id) is { GameDays.Count: > 0 } player)
            {
                freeAgents.Add(player);
            }
        }

        var plan = StreamingPlanner.Plan(roster, freeAgents, league.RosterSlots, league.Cadence, horizon, league.WeeklyAcquisitionLimit);
        return new StreamingPlanView(today, weekEnd, league.Name, plan.AcquisitionLimit, Math.Round(plan.BaselineValue, 1),
            Math.Round(plan.PlannedValue, 1), freeAgents.Count,
            plan.Moves.Select(move => new StreamingMoveView(move.Day, move.Add.Id.Value, move.Add.Name, move.Drop.Id.Value, move.Drop.Name,
                Math.Round(move.Gain, 1), move.AddUsableDays, move.DropUsableDaysLost, move.AcquisitionsUsed)).ToArray(),
            [.. plan.Evidence,
                $"Per-game value: average of each player's last {RecentGames} games under this league's scoring.",
                "Acquisitions already used this week are not tracked; the plan assumes the full limit is available."]);
    }

    private static DateOnly PlayedOn(DateTimeOffset startsAt) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, GameDateZone).DateTime);

    private static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), GameDateZone), TimeSpan.Zero);
}

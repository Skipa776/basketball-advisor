using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Landing;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Leagues;

public sealed record MatchupPlayer(Guid PlayerId, string Name, decimal ScoredSoFar, int GamesLeft,
    decimal? PerGame, decimal ProjectedRest, string? Injury = null);

public sealed record MatchupSide(Guid TeamId, string Name, decimal ScoredSoFar, decimal ProjectedRest,
    decimal ProjectedTotal, IReadOnlyList<MatchupPlayer> Players);

public sealed record Matchup(DateOnly WeekStart, DateOnly WeekEnd, DateOnly Today, string Scoring,
    int RecentGames, MatchupSide You, MatchupSide Opponent, IReadOnlyList<RosterTeamView> Opponents,
    string AvailabilityNote = "");

/// <summary>
/// Weekly points matchup: each side's points scored Monday through yesterday, plus the rest of
/// the week projected as games left × the player's average over their last <see cref="RecentGames"/>
/// appearances, all under the league's own scoring. Weeks run Monday–Sunday (US Eastern dates).
/// </summary>
public sealed class MatchupService(
    ILeagueRepository leagues, ILeagueTeamRepository teams, IPlayerRepository players,
    IGameRepository games, IBoxScoreRepository boxScores, LandingOptions options, TimeProvider clock,
    IAvailabilityRepository availability)
{
    public const int RecentGames = 10;
    private static readonly TimeZoneInfo GameDateZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly PointsScoringEngine points = new();

    public async Task<Matchup> WeekAsync(Guid leagueId, Guid? opponentId, DateOnly? day, CancellationToken token)
    {
        var league = await leagues.GetAsync(leagueId, token) ?? throw new ResourceNotFoundException("League was not found.");
        if (league.Type != LeagueType.Points)
        {
            throw new ResourceConflictException("The matchup uses points scoring; category leagues are not supported yet.");
        }

        var rosters = await teams.ListAsync(leagueId, token);
        var mine = rosters.SingleOrDefault(team => team.IsUsersTeam)
            ?? throw new ResourceConflictException("Import your league's rosters and mark your team (Mine = yes) first.");
        var others = rosters.Where(team => team.Id != mine.Id).ToArray();
        var opponent = (opponentId is { } id ? others.SingleOrDefault(team => team.Id == id) : others.FirstOrDefault())
            ?? throw new ResourceConflictException("Pick an opponent from your league's rosters.");

        var today = day ?? options.AsOf ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), GameDateZone).DateTime);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        var season = today.Month >= 8 ? today.Year + 1 : today.Year;
        var history = (await boxScores.ListAsync(season, DataSourceName.BasketballReference, NbaGamePhase.RegularSeason, today.AddDays(-1), token))
            .Where(sample => sample.DidPlay).ToArray();
        var schedule = await games.ListScheduledAsync(DataSourceName.BallDontLie,
            StartOfDayUtc(today), StartOfDayUtc(weekEnd.AddDays(1)), token);
        var injuries = await availability.ListAsync(token);

        async Task<MatchupSide> Side(LeagueTeam team)
        {
            var rows = new List<MatchupPlayer>(team.Players.Count);
            foreach (var playerId in team.Players)
            {
                var player = await players.GetAsync(playerId, token);
                var appearances = history.Where(sample => sample.PlayerId == playerId).OrderBy(sample => sample.PlayedOn).ToArray();
                var scored = appearances.Where(sample => sample.PlayedOn >= weekStart).Sum(sample => points.Score(sample.Statistics!, league));
                var recent = appearances.TakeLast(RecentGames).Select(sample => points.Score(sample.Statistics!, league)).ToArray();
                decimal? perGame = recent.Length == 0 ? null : Math.Round(recent.Average(), 2);
                // An out, IR or suspended player (per a current injury report) has no games left to count.
                var gamesLeft = player?.CurrentTeamId is { } teamId && !CurrentAvailability.MissesGames(injuries, playerId, today)
                    ? schedule.Count(game => game.HomeTeamId == teamId || game.AwayTeamId == teamId)
                    : 0;
                rows.Add(new MatchupPlayer(playerId.Value, player?.FullName ?? "Unknown player", scored, gamesLeft, perGame,
                    Math.Round(gamesLeft * (perGame ?? 0m), 2), CurrentAvailability.Applies(injuries, today) ? CurrentAvailability.Label(injuries, playerId) : null));
            }

            var soFar = rows.Sum(row => row.ScoredSoFar);
            var rest = rows.Sum(row => row.ProjectedRest);
            return new MatchupSide(team.Id, team.Name, soFar, rest, soFar + rest,
                rows.OrderByDescending(row => row.ProjectedRest + row.ScoredSoFar).ToArray());
        }

        return new Matchup(weekStart, weekEnd, today, league.Name, RecentGames, await Side(mine), await Side(opponent),
            others.Select(team => new RosterTeamView(team.Id, team.Name, false, [])).ToArray(),
            CurrentAvailability.Note(injuries, today));
    }

    private static DateTimeOffset StartOfDayUtc(DateOnly day) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), GameDateZone), TimeSpan.Zero);
}

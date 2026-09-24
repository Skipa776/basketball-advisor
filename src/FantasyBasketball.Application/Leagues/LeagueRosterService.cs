using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Leagues;

public sealed record RosterPlayer(Guid PlayerId, string Name);

public sealed record RosterTeamView(Guid Id, string Name, bool IsUsersTeam, IReadOnlyList<RosterPlayer> Players);

public sealed record RosterImportResult(int Teams, int Players, IReadOnlyList<string> UnmatchedPlayers,
    IReadOnlyList<RosterTeamView> Rosters);

/// <summary>League rosters from the CSV rung of the league import ladder.</summary>
public sealed class LeagueRosterService(
    ILeagueRepository leagues, ILeagueTeamRepository teams, IPlayerRepository players)
{
    public async Task<IReadOnlyList<RosterTeamView>> ListAsync(Guid leagueId, CancellationToken token)
    {
        await RequireLeagueAsync(leagueId, token);
        return await ViewsAsync(await teams.ListAsync(leagueId, token), token);
    }

    public async Task<RosterImportResult> ImportCsvAsync(Guid leagueId, string csv, CancellationToken token)
    {
        var league = await RequireLeagueAsync(leagueId, token);
        IReadOnlyList<RosterCsvRow> rows;
        try
        {
            rows = RosterCsv.Parse(csv);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(exception.Message, nameof(csv));
        }

        var byTeam = rows.GroupBy(row => row.Team, StringComparer.OrdinalIgnoreCase).ToArray();
        if (byTeam.Length > league.TeamCount)
        {
            throw new ArgumentException($"The CSV has {byTeam.Length} teams but the league has {league.TeamCount}.", nameof(csv));
        }

        var mine = byTeam.Where(group => group.Any(row => row.Mine)).ToArray();
        if (mine.Length > 1)
        {
            throw new ArgumentException("Mark only one team as yours.", nameof(csv));
        }

        var unmatched = new List<string>();
        var built = new List<LeagueTeam>();
        foreach (var group in byTeam)
        {
            var roster = new List<PlayerId>();
            foreach (var row in group)
            {
                // A name must identify exactly one canonical player; ambiguity is reported, never guessed.
                var matches = await players.FindByNormalizedNameAsync(PlayerName.Normalize(row.Player), token);
                if (matches.Count == 1 && !roster.Contains(matches[0].Id))
                {
                    roster.Add(matches[0].Id);
                }
                else if (matches.Count != 1)
                {
                    unmatched.Add($"{row.Player} (line {row.Line})");
                }
            }

            if (roster.Count > LeagueTeam.MaximumPlayers)
            {
                throw new ArgumentException($"{group.Key} lists more than {LeagueTeam.MaximumPlayers} players.", nameof(csv));
            }

            built.Add(new LeagueTeam(Guid.NewGuid(), group.First().Team, mine.Contains(group), roster));
        }

        await teams.ReplaceAsync(leagueId, built, token);
        return new RosterImportResult(built.Count, built.Sum(team => team.Players.Count), unmatched,
            await ViewsAsync(built, token));
    }

    private async Task<FantasyLeague> RequireLeagueAsync(Guid id, CancellationToken token) =>
        await leagues.GetAsync(id, token) ?? throw new ResourceNotFoundException("League was not found.");

    private async Task<IReadOnlyList<RosterTeamView>> ViewsAsync(IReadOnlyList<LeagueTeam> source, CancellationToken token)
    {
        var views = new List<RosterTeamView>(source.Count);
        foreach (var team in source)
        {
            var names = new List<RosterPlayer>(team.Players.Count);
            foreach (var id in team.Players)
            {
                names.Add(new RosterPlayer(id.Value, (await players.GetAsync(id, token))?.FullName ?? "Unknown player"));
            }

            views.Add(new RosterTeamView(team.Id, team.Name, team.IsUsersTeam, names));
        }

        return views;
    }
}

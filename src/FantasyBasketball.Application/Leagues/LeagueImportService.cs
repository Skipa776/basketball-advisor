using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Application.Leagues;

public sealed record ProviderTeamPreview(string ExternalTeamId, string Name, string? OwnerName, int Players);

public sealed record ProviderLeaguePreview(string Provider, string ExternalLeagueId, string Name, int TeamCount,
    IReadOnlyList<ProviderTeamPreview> Teams, IReadOnlyList<string> Notes);

public sealed record ProviderImportResult(int Teams, int Players, int PendingMatches, IReadOnlyList<string> Notes,
    IReadOnlyList<RosterTeamView> Rosters);

/// <summary>
/// Imports a platform league's rosters and per-league eligibility (league_import_contract).
/// Settings are compared and reported, never applied: scoring the app cannot represent exactly
/// is named, not approximated, and the user's own configuration stays authoritative.
/// </summary>
public sealed class LeagueImportService(
    ILeagueRepository leagues,
    ILeagueTeamRepository teams,
    ILeagueEligibilityRepository eligibility,
    ITeamRepository nbaTeams,
    PlayerIdentityResolver resolver,
    IFantasyLeagueProvider provider,
    LeagueRosterService rosters)
{
    private static readonly Dictionary<string, RosterSlotKind> Slots = new(StringComparer.Ordinal)
    {
        ["PG"] = RosterSlotKind.PG,
        ["SG"] = RosterSlotKind.SG,
        ["SF"] = RosterSlotKind.SF,
        ["PF"] = RosterSlotKind.PF,
        ["C"] = RosterSlotKind.C,
        ["G"] = RosterSlotKind.G,
        ["F"] = RosterSlotKind.F,
        ["UTIL"] = RosterSlotKind.UTIL,
        ["BN"] = RosterSlotKind.BENCH,
        ["IR"] = RosterSlotKind.IR,
    };

    private static readonly Dictionary<string, StatKey> Scoring = new(StringComparer.Ordinal)
    {
        ["pts"] = StatKey.PTS,
        ["reb"] = StatKey.REB,
        ["oreb"] = StatKey.OREB,
        ["dreb"] = StatKey.DREB,
        ["ast"] = StatKey.AST,
        ["stl"] = StatKey.STL,
        ["blk"] = StatKey.BLK,
        ["to"] = StatKey.TOV,
        ["tpm"] = StatKey.FG3M,
        ["tpa"] = StatKey.FG3A,
        ["fgm"] = StatKey.FGM,
        ["fga"] = StatKey.FGA,
        ["ftm"] = StatKey.FTM,
        ["fta"] = StatKey.FTA,
    };

    public async Task<ProviderLeaguePreview> PreviewAsync(Guid leagueId, string externalLeagueId, CancellationToken token)
    {
        var league = await RequireLeagueAsync(leagueId, token);
        var snapshot = await provider.GetLeagueAsync(externalLeagueId, token);
        return new ProviderLeaguePreview(snapshot.Provider, snapshot.ExternalLeagueId, snapshot.Name, snapshot.TeamCount,
            snapshot.Teams.Select(team => new ProviderTeamPreview(team.ExternalTeamId, team.Name, team.OwnerName, team.Players.Count)).ToArray(),
            Compare(league, snapshot));
    }

    public async Task<ProviderImportResult> ImportAsync(Guid leagueId, string externalLeagueId, string myExternalTeamId, CancellationToken token)
    {
        var league = await RequireLeagueAsync(leagueId, token);
        var snapshot = await provider.GetLeagueAsync(externalLeagueId, token);
        if (snapshot.Teams.All(team => team.ExternalTeamId != myExternalTeamId))
        {
            throw new ArgumentException("Pick which of the league's teams is yours.", nameof(myExternalTeamId));
        }

        if (snapshot.Teams.Count > league.TeamCount)
        {
            throw new ArgumentException($"The {snapshot.Provider} league has {snapshot.Teams.Count} teams but this league has {league.TeamCount}.", nameof(externalLeagueId));
        }

        var built = new List<LeagueTeam>();
        var positions = new Dictionary<PlayerId, IReadOnlyList<string>>();
        var pending = 0;
        foreach (var team in snapshot.Teams)
        {
            var roster = new List<PlayerId>();
            foreach (var external in team.Players)
            {
                var nbaTeam = external.TeamAbbreviation is { } abbreviation ? await nbaTeams.FindByAbbreviationAsync(abbreviation, token) : null;
                var resolution = await resolver.ResolveAsync(
                    new ExternalPlayer(external.ExternalPlayerId, external.FullName, nbaTeam?.Id, external.Positions, null, external.Provenance),
                    token);
                if (resolution.Player is not { } player)
                {
                    pending++;
                    continue;
                }

                if (!roster.Contains(player.Id))
                {
                    roster.Add(player.Id);
                }

                if (external.Positions.Count > 0)
                {
                    positions[player.Id] = external.Positions;
                }
            }

            built.Add(new LeagueTeam(Guid.NewGuid(), team.Name, team.ExternalTeamId == myExternalTeamId,
                roster.ToArray()));
        }

        await teams.ReplaceAsync(leagueId, built, token);
        if (positions.Count > 0)
        {
            await eligibility.SaveAsync(leagueId, positions, token);
        }

        return new ProviderImportResult(built.Count, built.Sum(team => team.Players.Count), pending, Compare(league, snapshot),
            await rosters.ListAsync(leagueId, token));
    }

    /// <summary>Every setting that differs or cannot be represented, stated and left unchanged.</summary>
    public static IReadOnlyList<string> Compare(FantasyLeague league, ExternalLeagueSnapshot snapshot)
    {
        var notes = new List<string>();
        if (snapshot.TeamCount != league.TeamCount)
        {
            notes.Add($"{snapshot.Provider} has {snapshot.TeamCount} teams; this league is set to {league.TeamCount} (not changed).");
        }

        var unknownSlots = snapshot.RosterSlots.Where(slot => !Slots.ContainsKey(slot)).Distinct().ToArray();
        if (unknownSlots.Length > 0)
        {
            notes.Add($"Lineup slots this app does not model: {string.Join(", ", unknownSlots)}.");
        }

        var theirs = snapshot.RosterSlots.Where(Slots.ContainsKey).Select(slot => Slots[slot]).OrderBy(kind => kind).ToArray();
        if (!theirs.SequenceEqual(league.RosterSlots.Select(slot => slot.Kind).OrderBy(kind => kind)))
        {
            notes.Add($"{snapshot.Provider} lineup: {string.Join(", ", snapshot.RosterSlots)}. This league: {string.Join(", ", league.RosterSlots.Select(slot => slot.Kind))} (not changed; edit League settings to match).");
        }

        var unmapped = snapshot.Scoring.Where(entry => entry.Value != 0m && !Scoring.ContainsKey(entry.Key)).Select(entry => entry.Key).OrderBy(key => key).ToArray();
        if (unmapped.Length > 0)
        {
            notes.Add($"Scoring not imported: {snapshot.Provider} also scores {string.Join(", ", unmapped)}, which this app does not track, so its scoring cannot be represented exactly. Keep this league's scoring set by hand.");
        }
        else
        {
            var differences = snapshot.Scoring.Where(entry => Scoring.ContainsKey(entry.Key))
                .Where(entry => league.ScoringRules.FirstOrDefault(rule => rule.Stat == Scoring[entry.Key])?.PointsPerUnit != entry.Value)
                .Select(entry => $"{Scoring[entry.Key]} {entry.Value}").ToArray();
            if (differences.Length > 0)
            {
                notes.Add($"{snapshot.Provider} scoring differs: {string.Join(", ", differences)} (not changed; edit League settings to match).");
            }
        }

        return notes;
    }

    private async Task<FantasyLeague> RequireLeagueAsync(Guid id, CancellationToken token) =>
        await leagues.GetAsync(id, token) ?? throw new ResourceNotFoundException("League was not found.");
}

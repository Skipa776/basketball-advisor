using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Abstractions;

public sealed record ExternalRosterPlayer(
    string ExternalPlayerId, string FullName, string? TeamAbbreviation, IReadOnlyList<string> Positions, DataProvenance Provenance);

public sealed record ExternalLeagueTeam(
    string ExternalTeamId, string Name, string? OwnerName, IReadOnlyList<ExternalRosterPlayer> Players);

/// <summary>
/// The provider-neutral league snapshot (league_import_contract). Settings are carried raw so
/// the importer can say exactly which ones cannot be represented; the domain never learns the provider.
/// </summary>
public sealed record ExternalLeagueSnapshot(
    string Provider,
    string ExternalLeagueId,
    string Name,
    int TeamCount,
    IReadOnlyList<string> RosterSlots,
    IReadOnlyDictionary<string, decimal> Scoring,
    IReadOnlyList<ExternalLeagueTeam> Teams);

public sealed record ExternalDraftPick(int PickNumber, string ExternalPlayerId, string FullName);

/// <summary>The league's most recent draft with its picks so far, as the provider lists them.</summary>
public sealed record ExternalDraft(
    string ExternalDraftId, string Status, int TeamCount, int RoundCount, IReadOnlyList<ExternalDraftPick> Picks);

public interface IFantasyLeagueProvider : IDataSource
{
    Task<ExternalLeagueSnapshot> GetLeagueAsync(string externalLeagueId, CancellationToken cancellationToken);

    Task<ExternalDraft?> GetLatestDraftAsync(string externalLeagueId, CancellationToken cancellationToken);
}

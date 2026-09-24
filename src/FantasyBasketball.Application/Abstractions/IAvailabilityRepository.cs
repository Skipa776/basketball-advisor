using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Abstractions;

public sealed record ExternalAvailability(
    string ExternalPlayerId, string FullName, string? TeamAbbreviation, string StatusCode,
    string? BodyPart, string? Notes, DateTimeOffset? ReportedAt, DataProvenance Provenance);

/// <summary>A provider's current injury report for every player it lists as injured.</summary>
public interface IAvailabilitySource : IDataSource
{
    Task<IReadOnlyList<ExternalAvailability>> GetInjuriesAsync(CancellationToken cancellationToken);
}

/// <summary>Current availability per player: one report per source replaces the previous one.</summary>
public interface IAvailabilityRepository
{
    Task ReplaceAsync(string source, IReadOnlyList<PlayerAvailability> reports, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<PlayerId, PlayerAvailability>> ListAsync(CancellationToken cancellationToken);
}

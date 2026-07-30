using FantasyBasketball.Domain.Draft;

namespace FantasyBasketball.Application.Abstractions;

public sealed record DraftSessionRecord(DraftSession Session, Guid LeagueId);

public interface IDraftRepository
{
    Task AddSessionAsync(
        DraftSession session,
        Guid leagueId,
        CancellationToken cancellationToken);

    Task<DraftSessionRecord?> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task AddPickAsync(
        DraftPick pick,
        CancellationToken cancellationToken);

    Task RemoveLastPickAsync(
        Guid draftSessionId,
        int pickNumber,
        CancellationToken cancellationToken);
}

public interface IDraftCandidateRepository
{
    Task<IReadOnlyList<DraftCandidate>> ListAsync(
        Guid leagueId,
        CancellationToken cancellationToken);
}

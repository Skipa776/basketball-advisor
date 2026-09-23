using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Application.Common;

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

    Task<PagedResult<DraftSessionRecord>> ListAsync(
        Guid leagueId,
        int page,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> HasAnyForLeagueAsync(
        Guid leagueId,
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

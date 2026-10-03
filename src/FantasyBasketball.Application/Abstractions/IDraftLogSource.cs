using FantasyBasketball.Domain.Draft;

namespace FantasyBasketball.Application.Abstractions;

/// <summary>A platform's public completed drafts, reached user by user (scraping_policy).</summary>
public interface IDraftLogSource
{
    string Name { get; }

    /// <summary>User ids to start from: a league's members, or the one user a username names.</summary>
    Task<IReadOnlyList<string>> SeedUsersAsync(string seed, CancellationToken cancellationToken);

    Task<IReadOnlyList<DraftSummary>> ListUserDraftsAsync(string userId, int season, CancellationToken cancellationToken);

    Task<DraftLog> GetDraftAsync(DraftSummary draft, CancellationToken cancellationToken);
}

/// <summary>What a user's draft list says about a draft, enough to decide whether to fetch its picks.</summary>
public sealed record DraftSummary(
    string DraftId,
    int Season,
    string Status,
    string Type,
    int TeamCount,
    int Rounds,
    string? ScoringType,
    IReadOnlyDictionary<string, int> Slots,
    DateTimeOffset StartedAt);

public interface IDraftLogRepository
{
    Task<bool> ExistsAsync(string source, string draftId, CancellationToken cancellationToken);

    Task AddAsync(DraftLog log, CancellationToken cancellationToken);

    Task<int> CountAsync(string source, int season, CancellationToken cancellationToken);
}

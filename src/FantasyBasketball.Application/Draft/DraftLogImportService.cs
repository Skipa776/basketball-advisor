using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Draft;

namespace FantasyBasketball.Application.Draft;

public sealed record DraftLogImportResult(int Imported, int Skipped, int UsersVisited, int TotalStored);

/// <summary>
/// Collects completed public snake drafts by snowball: from the seed's users, each user's drafts
/// for the season, then the users who picked in those drafts, breadth first, until
/// <c>maxDrafts</c> are stored or no users remain. Only complete 8–14-team snake drafts with
/// 10+ rounds and no keepers are kept, so the fit sees ordinary redraft behaviour.
/// </summary>
public sealed class DraftLogImportService(IDraftLogSource source, IDraftLogRepository logs)
{
    public const int MinTeams = 8;
    public const int MaxTeams = 14;
    public const int MinRounds = 10;

    public async Task<DraftLogImportResult> ImportAsync(string seed, int season, int maxDrafts, int maxUsers, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDrafts, 1);
        var queue = new Queue<string>(await source.SeedUsersAsync(seed, cancellationToken));
        var seenUsers = new HashSet<string>(queue, StringComparer.Ordinal);
        var seenDrafts = new HashSet<string>(StringComparer.Ordinal);
        var stored = await logs.CountAsync(source.Name, season, cancellationToken);
        int imported = 0, skipped = 0, visited = 0;
        while (queue.Count > 0 && stored < maxDrafts && visited < maxUsers)
        {
            var user = queue.Dequeue();
            visited++;
            foreach (var summary in await source.ListUserDraftsAsync(user, season, cancellationToken))
            {
                if (stored >= maxDrafts || !seenDrafts.Add(summary.DraftId))
                {
                    continue;
                }

                if (!IsUsable(summary) || await logs.ExistsAsync(source.Name, summary.DraftId, cancellationToken))
                {
                    skipped++;
                    continue;
                }

                var log = await source.GetDraftAsync(summary, cancellationToken);
                if (log.Picks.Count < log.TeamCount * log.Rounds)
                {
                    skipped++; // keepers or an unfinished board leave holes
                    continue;
                }

                await logs.AddAsync(log, cancellationToken);
                imported++;
                stored++;
                foreach (var next in log.Picks.Select(pick => pick.PickedBy).OfType<string>())
                {
                    if (next.Length > 0 && seenUsers.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return new DraftLogImportResult(imported, skipped, visited, stored);
    }

    private static bool IsUsable(DraftSummary draft) =>
        draft is { Status: "complete", Type: "snake", TeamCount: >= MinTeams and <= MaxTeams, Rounds: >= MinRounds };
}

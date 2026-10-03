using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class DraftLogRepository(FantasyDbContext database) : IDraftLogRepository
{
    public Task<bool> ExistsAsync(string source, string draftId, CancellationToken cancellationToken) =>
        database.DraftLogs.AnyAsync(row => row.Source == source && row.DraftId == draftId, cancellationToken);

    public Task<int> CountAsync(string source, int season, CancellationToken cancellationToken) =>
        database.DraftLogs.CountAsync(row => row.Source == source && row.Season == season, cancellationToken);

    public async Task AddAsync(DraftLog log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        database.DraftLogs.Add(DraftLogRow.Create(
            Guid.NewGuid(),
            log.Source,
            log.DraftId,
            log.Season,
            log.TeamCount,
            log.Rounds,
            log.ScoringType,
            JsonSerializer.Serialize(log.Slots),
            log.StartedAt,
            log.Picks.Select(pick => DraftLogPickRow.Create(
                pick.PickNumber, pick.Round, pick.DraftSlot, pick.PlayerId, pick.PlayerName, [.. pick.Positions], pick.PickedBy))));
        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear(); // a crawl adds hundreds of drafts; keep the tracker small
    }
}

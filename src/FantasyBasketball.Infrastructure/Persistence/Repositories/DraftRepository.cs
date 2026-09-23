using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class DraftRepository(FantasyDbContext database) : IDraftRepository
{
    public async Task<PagedResult<DraftSessionRecord>> ListAsync(
        Guid leagueId,
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = database.DraftSessions.AsNoTracking()
            .Where(value => value.FantasyLeagueId == leagueId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(value => value.Id)
            .Skip((int)Math.Min((long)(page - 1) * limit, int.MaxValue))
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        var rowIds = rows.Select(value => value.Id).ToArray();
        var picks = rowIds.Length == 0
            ? []
            : await database.DraftPicks.AsNoTracking()
                .Where(value => rowIds.Contains(value.DraftSessionId))
                .OrderBy(value => value.DraftSessionId)
                .ThenBy(value => value.PickNumber)
                .Select(value => new DraftPick(
                    value.Id,
                    value.DraftSessionId,
                    new PlayerId(value.PlayerId),
                    value.PickNumber))
                .ToArrayAsync(cancellationToken);
        var result = new List<DraftSessionRecord>(rows.Length);
        foreach (var row in rows)
        {
            result.Add(new DraftSessionRecord(
                new DraftSession(
                    row.Id,
                    row.TeamCount,
                    row.RoundCount,
                    row.UserSlot,
                    picks.Where(pick => pick.DraftSessionId == row.Id).ToArray()),
                row.FantasyLeagueId));
        }

        return new PagedResult<DraftSessionRecord>(result, total, page, limit);
    }

    public Task<bool> HasAnyForLeagueAsync(
        Guid leagueId,
        CancellationToken cancellationToken) =>
        database.DraftSessions.AsNoTracking()
            .AnyAsync(value => value.FantasyLeagueId == leagueId, cancellationToken);

    public async Task AddSessionAsync(
        DraftSession session,
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (leagueId == Guid.Empty)
        {
            throw new ArgumentException("League id cannot be empty.", nameof(leagueId));
        }

        database.DraftSessions.Add(DraftSessionRow.Create(
            session.Id,
            leagueId,
            session.RoundCount,
            session.TeamCount,
            session.UserSlot));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<DraftSessionRecord?> GetSessionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await database.DraftSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var picks = await database.DraftPicks
            .AsNoTracking()
            .Where(value => value.DraftSessionId == id)
            .OrderBy(value => value.PickNumber)
            .Select(value => new DraftPick(
                value.Id,
                value.DraftSessionId,
                new PlayerId(value.PlayerId),
                value.PickNumber))
            .ToArrayAsync(cancellationToken);
        return new DraftSessionRecord(
            new DraftSession(
                row.Id,
                row.TeamCount,
                row.RoundCount,
                row.UserSlot,
                picks),
            row.FantasyLeagueId);
    }

    public async Task AddPickAsync(
        DraftPick pick,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pick);
        database.DraftPicks.Add(DraftPickRow.Create(
            pick.Id,
            pick.DraftSessionId,
            pick.PlayerId.Value,
            pick.PickNumber));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ResourceConflictException(
                $"Pick {pick.PickNumber} was submitted concurrently: "
                + exception.GetType().Name);
        }
    }

    public async Task RemoveLastPickAsync(
        Guid draftSessionId,
        int pickNumber,
        CancellationToken cancellationToken)
    {
        var row = await database.DraftPicks.SingleOrDefaultAsync(
            value => value.DraftSessionId == draftSessionId
                && value.PickNumber == pickNumber,
            cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"Pick {pickNumber} was not found.");
        var latest = await database.DraftPicks
            .Where(value => value.DraftSessionId == draftSessionId)
            .MaxAsync(value => value.PickNumber, cancellationToken);
        if (latest != pickNumber)
        {
            throw new ResourceConflictException(
                "Only the most recent pick can be undone.");
        }

        database.DraftPicks.Remove(row);
        await database.SaveChangesAsync(cancellationToken);
    }
}

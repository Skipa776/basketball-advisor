using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class PlayerRepository(FantasyDbContext database)
    : IPlayerRepository, IPlayerQueryRepository
{
    public async Task AddAsync(Player player, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(player);
        database.Players.Add(PlayerRow.Create(
            player.Id.Value,
            player.FullName,
            player.NormalizedName,
            player.Positions.ToArray(),
            player.BirthDate,
            player.CurrentTeamId?.Value));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken)
    {
        var row = await database.Players.SingleAsync(value => value.Id == id.Value, cancellationToken);
        row.MoveToTeam(teamId?.Value);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken)
    {
        var row = await database.Players
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id.Value, cancellationToken);

        return row is null
            ? null
            : new Player(
                new PlayerId(row.Id),
                row.FullName,
                row.NormalizedName,
                row.CurrentTeamId is { } teamId ? new NbaTeamId(teamId) : null,
                row.Positions,
                row.BirthDate);
    }

    public async Task<Player?> FindByExternalIdentityAsync(
        string provider,
        string externalId,
        CancellationToken cancellationToken)
    {
        var row = await database.ExternalPlayerIdentities
            .AsNoTracking()
            .Where(identity => identity.Provider == provider && identity.ExternalId == externalId)
            .Join(
                database.Players.AsNoTracking(),
                identity => identity.PlayerId,
                player => player.Id,
                (_, player) => player)
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken) =>
        await database.Players
            .AsNoTracking()
            .Where(player => player.NormalizedName == normalizedName)
            .Select(player => ToDomain(player))
            .ToArrayAsync(cancellationToken);

    public async Task<ExternalPlayerIdentity?> FindIdentityAsync(
        PlayerId playerId,
        string provider,
        CancellationToken cancellationToken)
    {
        var row = await database.ExternalPlayerIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                identity => identity.PlayerId == playerId.Value
                    && identity.Provider == provider,
                cancellationToken);

        return row is null
            ? null
            : new ExternalPlayerIdentity(
                new PlayerId(row.PlayerId),
                row.Provider,
                row.ExternalId,
                row.LinkedAt,
                row.ConfirmedByHuman);
    }

    public async Task AddResolvedIdentityAsync(
        Player player,
        ExternalPlayerIdentity identity,
        bool addPlayer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(identity);

        if (identity.PlayerId != player.Id)
        {
            throw new ArgumentException(
                "The external identity must belong to the supplied player.",
                nameof(identity));
        }

        if (addPlayer)
        {
            database.Players.Add(ToRow(player));
        }

        database.ExternalPlayerIdentities.Add(ExternalPlayerIdentityRow.Create(
            identity.PlayerId.Value,
            identity.Provider,
            identity.ExternalId,
            identity.LinkedAt,
            identity.ConfirmedByHuman));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AddPendingIdentityMatchAsync(
        PendingIdentityMatch pendingMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingMatch);
        database.PendingIdentityMatches.Add(PendingIdentityMatchRow.Create(
            pendingMatch.Id,
            pendingMatch.Provider,
            pendingMatch.ExternalId,
            pendingMatch.FullName,
            pendingMatch.NormalizedName,
            pendingMatch.CandidatePlayerIds.Select(value => value.Value).ToArray(),
            pendingMatch.CreatedAt,
            pendingMatch.Reason));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Player>> ListAsync(
        string? search,
        string? team,
        string? position,
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = database.Players.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(player =>
                EF.Functions.ILike(player.FullName, pattern)
                || EF.Functions.ILike(player.NormalizedName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(team))
        {
            var abbreviation = team.Trim().ToUpperInvariant();
            query = query.Where(player => database.NbaTeams.Any(nbaTeam =>
                nbaTeam.Id == player.CurrentTeamId
                && nbaTeam.Abbreviation == abbreviation));
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            var normalizedPosition = position.Trim().ToUpperInvariant();
            query = query.Where(player =>
                player.Positions.Contains(normalizedPosition));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(player => player.FullName)
            .ThenBy(player => player.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(player => ToDomain(player))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<Player>(items, total, page, limit);
    }

    private static Player ToDomain(PlayerRow row) =>
        new(
            new PlayerId(row.Id),
            row.FullName,
            row.NormalizedName,
            row.CurrentTeamId is { } teamId ? new NbaTeamId(teamId) : null,
            row.Positions,
            row.BirthDate);

    private static PlayerRow ToRow(Player player) =>
        PlayerRow.Create(
            player.Id.Value,
            player.FullName,
            player.NormalizedName,
            player.Positions.ToArray(),
            player.BirthDate,
            player.CurrentTeamId?.Value);
}

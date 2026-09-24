using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class GameRepository(FantasyDbContext database) : IGameRepository
{
    public async Task AddAsync(NbaGame game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        var externalId = game.Provenance.ExternalId
            ?? throw new ArgumentException(
                "Imported game provenance requires an external id.",
                nameof(game));
        database.NbaGames.Add(NbaGameRow.Create(
            game.Id,
            game.SeasonEndYear,
            game.StartsAt,
            game.HomeTeamId.Value,
            game.AwayTeamId.Value,
            game.HomeScore,
            game.AwayScore,
            game.Status,
            game.Provenance.Source,
            externalId,
            game.Provenance.FetchedAt,
            game.Provenance.SourceTimestamp,
            game.Provenance.ParserVersion,
            game.Provenance.Confidence,
            game.Provenance.RawRecordHash));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveResultAsync(NbaGame latest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(latest);
        var provenance = latest.Provenance;
        var row = await database.NbaGames.SingleAsync(
            game => game.Source == provenance.Source && game.ExternalId == provenance.ExternalId,
            cancellationToken);
        row.RecordResult(latest.StartsAt, latest.HomeScore, latest.AwayScore, latest.Status,
            provenance.FetchedAt, provenance.SourceTimestamp, provenance.ParserVersion,
            provenance.Confidence, provenance.RawRecordHash);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledGame>> ListFinalAsync(
        string source,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var rows = await database.NbaGames
            .AsNoTracking()
            .Where(game => game.Source == source && game.Status == "Final"
                && game.StartsAt >= fromUtc && game.StartsAt < toUtc)
            .Join(database.NbaTeams, game => game.HomeTeamId, team => team.Id,
                (game, team) => new { Game = game, team.Abbreviation })
            .OrderBy(row => row.Game.StartsAt)
            .ToArrayAsync(cancellationToken);
        return rows.Select(row => new ScheduledGame(ToDomain(row.Game), row.Abbreviation)).ToArray();
    }

    public async Task<NbaGame?> GetBySourceAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken)
    {
        var row = await database.NbaGames
            .AsNoTracking()
            .SingleOrDefaultAsync(
                game => game.Source == source && game.ExternalId == externalId,
                cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    private static NbaGame ToDomain(NbaGameRow row) =>
        new(
            row.Id,
            row.SeasonEndYear,
            row.StartsAt,
            new NbaTeamId(row.HomeTeamId),
            new NbaTeamId(row.AwayTeamId),
            row.HomeScore,
            row.AwayScore,
            row.Status,
            new DataProvenance(
                row.Source,
                row.ExternalId,
                row.FetchedAt,
                row.SourceTimestamp,
                row.ParserVersion,
                row.Confidence,
                row.RawRecordHash));
}

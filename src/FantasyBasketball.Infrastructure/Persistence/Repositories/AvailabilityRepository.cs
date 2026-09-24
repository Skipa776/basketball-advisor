using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class AvailabilityRepository(FantasyDbContext database) : IAvailabilityRepository
{
    public async Task ReplaceAsync(string source, IReadOnlyList<PlayerAvailability> reports, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.PlayerAvailability.Where(row => row.Source == source).ExecuteDeleteAsync(cancellationToken);
        database.PlayerAvailability.AddRange(reports.Select(PlayerAvailabilityRow.Create));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<PlayerId, PlayerAvailability>> ListAsync(CancellationToken cancellationToken) =>
        (await database.PlayerAvailability.AsNoTracking().ToArrayAsync(cancellationToken))
        .GroupBy(row => row.PlayerId)
        .Select(group => group.OrderByDescending(row => row.FetchedAt).First())
        .ToDictionary(row => new PlayerId(row.PlayerId), row => new PlayerAvailability(new PlayerId(row.PlayerId),
            Enum.Parse<AvailabilityStatus>(row.Status), row.BodyPart, row.Notes, row.ReportedAt,
            new DataProvenance(row.Source, row.ExternalId, row.FetchedAt, row.SourceTimestamp, row.ParserVersion, row.Confidence, row.RawRecordHash)));
}

using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using CanonicalAdpEntry = FantasyBasketball.Domain.Draft.AdpEntry;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class AdpRepository(FantasyDbContext database) : IAdpRepository
{
    public async Task AddAsync(
        CanonicalAdpEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var provenance = entry.Provenance;
        var externalId = provenance.ExternalId
            ?? throw new ArgumentException(
                "ADP provenance requires an external id.",
                nameof(entry));

        database.AdpEntries.Add(AdpEntryRow.Create(
            entry.Id,
            entry.PlayerId.Value,
            entry.AverageDraftPosition,
            entry.StandardDeviation,
            provenance.Source,
            externalId,
            provenance.FetchedAt,
            provenance.SourceTimestamp,
            provenance.ParserVersion,
            provenance.Confidence,
            provenance.RawRecordHash));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<CanonicalAdpEntry?> GetLatestAsync(
        PlayerId playerId,
        CancellationToken cancellationToken)
    {
        var row = await database.AdpEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerId == playerId.Value)
            .OrderByDescending(entry => entry.FetchedAt)
            .ThenByDescending(entry => entry.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new CanonicalAdpEntry(
                row.Id,
                new PlayerId(row.PlayerId),
                row.AverageDraftPosition,
                row.StandardDeviation,
                new DataProvenance(
                    row.Source,
                    row.ExternalId,
                    row.FetchedAt,
                    row.SourceTimestamp,
                    row.ParserVersion,
                    row.Confidence,
                    row.RawRecordHash));
    }
}

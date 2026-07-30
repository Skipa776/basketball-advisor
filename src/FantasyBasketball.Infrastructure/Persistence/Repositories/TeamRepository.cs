using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class TeamRepository(FantasyDbContext database) : ITeamRepository
{
    public async Task AddAsync(
        NbaTeam team,
        DataProvenance provenance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(provenance);
        var externalId = provenance.ExternalId
            ?? throw new ArgumentException(
                "Imported team provenance requires an external id.",
                nameof(provenance));

        database.NbaTeams.Add(NbaTeamRow.Create(
            team.Id.Value,
            team.Name,
            team.Abbreviation));
        database.NbaTeamSources.Add(NbaTeamSourceRow.Create(
            team.Id.Value,
            provenance.Source,
            externalId,
            provenance.FetchedAt,
            provenance.SourceTimestamp,
            provenance.ParserVersion,
            provenance.Confidence,
            provenance.RawRecordHash));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<NbaTeam?> FindByAbbreviationAsync(
        string abbreviation,
        CancellationToken cancellationToken)
    {
        var row = await database.NbaTeams
            .AsNoTracking()
            .SingleOrDefaultAsync(
                team => team.Abbreviation == abbreviation,
                cancellationToken);
        return row is null
            ? null
            : new NbaTeam(new NbaTeamId(row.Id), row.Name, row.Abbreviation);
    }
}

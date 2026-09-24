using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task Directory_import_moves_a_traded_player_to_the_current_team()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateDatabase();
        var clippers = Guid.NewGuid();
        var cavaliers = Guid.NewGuid();
        database.NbaTeams.AddRange(NbaTeamRow.Create(clippers, "Clippers", "LAC"), NbaTeamRow.Create(cavaliers, "Cavaliers", "CLE"));
        database.Players.Add(PlayerRow.Create(Guid.NewGuid(), "Trade Candidate", "trade candidate", ["G"], null, clippers));
        await database.SaveChangesAsync(token);
        var repository = new PlayerRepository(database);
        var provenance = new DataProvenance(DataSourceName.BallDontLie, "7", DateTimeOffset.UnixEpoch, null, "balldontlie-v1", 1m, new string('c', 64));
        var service = new ImportPlayersService(new PlayerIdentityResolver(repository, new FixedTimeProvider(DateTimeOffset.UnixEpoch)),
            repository, new DataImportRunRepository(database), new EfImportTransaction(database), new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        await service.ImportAsync(DataSourceName.BallDontLie,
            [new ExternalPlayer("7", "Trade Candidate", new NbaTeamId(cavaliers), ["G"], null, provenance)], token);

        database.ChangeTracker.Clear();
        (await database.Players.SingleAsync(token)).CurrentTeamId.ShouldBe(cavaliers);
        (await database.Players.CountAsync(token)).ShouldBe(1, "the directory row resolved to the existing player");
    }
}

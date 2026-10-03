using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task DL05_draft_logs_store_once_per_source_and_draft_with_every_pick()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateDatabase();
        var repository = new DraftLogRepository(database);
        var log = new DraftLog(DataSourceName.Sleeper, "d1", 2025, 2, 1, "points",
            new Dictionary<string, int> { ["UTIL"] = 1 }, DateTimeOffset.UnixEpoch,
            [new DraftLogPick(1, 1, 1, "4", "Nikola Jokic", ["C"], "u1"), new DraftLogPick(2, 1, 2, "5", "Luka Doncic", ["PG", "SG"], null)]);

        await repository.AddAsync(log, token);

        (await repository.ExistsAsync(DataSourceName.Sleeper, "d1", token)).ShouldBeTrue();
        (await repository.CountAsync(DataSourceName.Sleeper, 2025, token)).ShouldBe(1);
        var picks = await database.Set<FantasyBasketball.Infrastructure.Persistence.Entities.DraftLogPickRow>().AsNoTracking()
            .OrderBy(pick => pick.PickNumber).ToArrayAsync(token);
        picks.Select(pick => pick.Positions.Length).ShouldBe([1, 2]);
        picks[1].PickedBy.ShouldBeNull();
        await Should.ThrowAsync<DbUpdateException>(() => repository.AddAsync(log, token));
    }
}

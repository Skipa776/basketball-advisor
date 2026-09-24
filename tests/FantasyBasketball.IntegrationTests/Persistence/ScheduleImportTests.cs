using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task Schedule_reimport_moves_a_scheduled_game_to_final_and_skips_unchanged_games()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateDatabase();
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();
        database.NbaTeams.AddRange(NbaTeamRow.Create(home, "Home", "HOM"), NbaTeamRow.Create(away, "Away", "AWY"));
        await database.SaveChangesAsync(token);
        var tipOff = new DateTimeOffset(2026, 10, 21, 23, 30, 0, TimeSpan.Zero);
        var service = new ImportScheduleService(new GameRepository(database), new DataImportRunRepository(database),
            new EfImportTransaction(database), new FixedTimeProvider(tipOff));

        (await service.ImportFromAsync(new OneGame(Game(home, away, tipOff, "Scheduled", null, null, 'a')), new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21), null, token))
            .RowsWritten.ShouldBe(1);
        var final = Game(home, away, tipOff, "Final", 112, 104, 'b');
        (await service.ImportFromAsync(new OneGame(final), new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21), null, token))
            .RowsWritten.ShouldBe(1, "the stored game changed");
        (await service.ImportFromAsync(new OneGame(final), new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21), null, token))
            .RowsWritten.ShouldBe(0, "nothing changed");

        database.ChangeTracker.Clear();
        var row = await database.NbaGames.SingleAsync(token);
        row.Status.ShouldBe("Final");
        row.HomeScore.ShouldBe(112);
        row.AwayScore.ShouldBe(104);
        row.RawRecordHash.ShouldBe(new string('b', 64), "provenance follows the data it describes");
        (await new GameRepository(database).ListFinalAsync(DataSourceName.BallDontLie, tipOff.AddHours(-1), tipOff.AddHours(1), token))
            .ShouldHaveSingleItem().HomeAbbreviation.ShouldBe("HOM");
    }

    private static NbaGame Game(Guid home, Guid away, DateTimeOffset tipOff, string status, int? homeScore, int? awayScore, char hash) =>
        new(Guid.NewGuid(), 2027, tipOff, new NbaTeamId(home), new NbaTeamId(away), homeScore, awayScore, status,
            new DataProvenance(DataSourceName.BallDontLie, "game-1", tipOff, null, "balldontlie-v1", 1m, new string(hash, 64)));

    private sealed class OneGame(NbaGame game) : IScheduleProvider
    {
        public string Name => DataSourceName.BallDontLie;
        public DataSourceKind Kind => DataSourceKind.Api;
        public Task<IReadOnlyList<NbaGame>> GetGamesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NbaGame>>([game]);
    }
}

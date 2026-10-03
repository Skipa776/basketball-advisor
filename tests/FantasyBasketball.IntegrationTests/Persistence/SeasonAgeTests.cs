using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task I16_reimport_fills_a_missing_age_and_never_overwrites_one()
    {
        var token = TestContext.Current.CancellationToken;
        var playerId = new PlayerId(Guid.NewGuid());
        await using (var database = CreateDatabase())
        {
            await new PlayerRepository(database).AddAsync(
                new Player(playerId, "Nikola Jokic", "nikola jokic", null, ["C"], null), token);
            await new SeasonStatLineRepository(database).AddAsync(AgeLine(playerId, null), token);
        }

        await ImportAsync(AgeLine(playerId, 30), token);
        await ImportAsync(AgeLine(playerId, 31), token);

        await using var check = CreateDatabase();
        var stored = await new SeasonStatLineRepository(check)
            .GetAsync(playerId, 2026, DataSourceName.BasketballReference, token);
        stored.ShouldNotBeNull().Age.ShouldBe(30);
    }

    private async Task ImportAsync(SeasonStatLine line, CancellationToken token)
    {
        await using var database = CreateDatabase();
        var run = await new ImportSeasonStatsService(
                new SeasonStatLineRepository(database),
                new DataImportRunRepository(database),
                new EfImportTransaction(database),
                new FixedTimeProvider(DateTimeOffset.UnixEpoch))
            .ImportFromAsync(new OneLineProvider(line), 2026, null, token);
        run.Status.ShouldBe(DataImportRunStatus.Succeeded);
    }

    private static SeasonStatLine AgeLine(PlayerId playerId, int? age) => new(
        playerId,
        2026,
        70,
        34m,
        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = 27m }),
        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = 1890m }),
        null,
        new DataProvenance(DataSourceName.BasketballReference, "jokicni01",
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero), null, "basketball-reference-v1", 1m, new string('c', 64)),
        age);

    private sealed class OneLineProvider(SeasonStatLine line) : IPlayerStatsProvider
    {
        public string Name => DataSourceName.BasketballReference;

        public DataSourceKind Kind => DataSourceKind.Scraper;

        public Task<IReadOnlyList<SeasonStatLine>> GetSeasonStatsAsync(int seasonEndYear, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SeasonStatLine>>([line]);
    }
}

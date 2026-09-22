using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;

namespace FantasyBasketball.IntegrationTests.TestSupport;

internal static class RecordedGameFixture
{
    public static async Task SeedAsync(FantasyDbContext database, Guid guard, Guid center, Guid? rookie, CancellationToken token)
    {
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();
        database.NbaTeams.AddRange(NbaTeamRow.Create(home, "Fixture Home", "AAA"), NbaTeamRow.Create(away, "Fixture Away", "BBB"));
        for (var day = 1; day <= 14; day++)
        {
            var id = Guid.NewGuid();
            var playedOn = new DateOnly(2026, 1, day);
            var fetched = new DateTimeOffset(2026, 1, day, 23, 0, 0, TimeSpan.Zero);
            var provenance = new DataProvenance(DataSourceName.Manual, id.ToString(), fetched, null,
                "manual-v1", DataSourceConfidence.ManualEntry, new string('a', 64));
            database.NbaGames.Add(NbaGameRow.Create(id, 2026, fetched.AddHours(-3), home, away, 100, 90,
                "Final", provenance.Source, id.ToString(), fetched, null, provenance.ParserVersion, provenance.Confidence, provenance.RawRecordHash));
            await database.SaveChangesAsync(token);
            var rows = new List<PlayerGameSample>
            {
                new(id, new PlayerId(guard), 2026, playedOn, true, day != 5,
                    day == 5 ? null : Stats(day <= 11 ? 10 : 20, 2), provenance),
                new(id, new PlayerId(center), 2026, playedOn, true, day != 5,
                    day == 5 ? null : Stats(30, 0), provenance),
            };
            if (rookie.HasValue && day >= 13)
                rows.Add(new PlayerGameSample(id, new PlayerId(rookie.Value), 2026, playedOn, true, true, Stats(50, 0), provenance));
            await new BoxScoreRepository(database).AddAsync(new CompletedBoxScore(id, 2026, playedOn,
                NbaGamePhase.RegularSeason, provenance, rows), token);
        }
    }

    private static StatLine Stats(decimal points, decimal assists) => new(new Dictionary<StatKey, decimal>
    {
        [StatKey.PTS] = points,
        [StatKey.AST] = assists,
    });
}

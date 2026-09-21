using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Trends;

public sealed class CompletedBoxScoreTests
{
    [Fact]
    public void BS06_snapshot_requires_coherent_distinct_final_observations()
    {
        var game = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 2);
        var provenance = new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch,
            null, "manual-v1", 1m, new string('a', 64));
        var sample = new PlayerGameSample(game, new PlayerId(Guid.NewGuid()), 2026, date, true, true,
            new StatLine(new Dictionary<StatKey, decimal>()), provenance);
        var rows = new List<PlayerGameSample> { sample };
        var result = new CompletedBoxScore(game, 2026, date, NbaGamePhase.RegularSeason, provenance, rows);
        rows.Clear();
        result.Samples.Count.ShouldBe(1);
        result.Phase.ShouldBe(NbaGamePhase.RegularSeason);
        Should.Throw<ArgumentException>(() => new CompletedBoxScore(game, 2026, date, NbaGamePhase.RegularSeason, provenance, []));
        Should.Throw<ArgumentException>(() => new CompletedBoxScore(game, 2026, date, NbaGamePhase.RegularSeason, provenance, [sample, sample]));
        Should.Throw<ArgumentException>(() => new CompletedBoxScore(Guid.NewGuid(), 2026, date, NbaGamePhase.RegularSeason, provenance, [sample]));
        Should.Throw<ArgumentException>(() => new CompletedBoxScore(game, 2025, date, NbaGamePhase.RegularSeason, provenance, [sample]));
        Should.Throw<ArgumentException>(() => new CompletedBoxScore(game, 2026, date.AddDays(1), NbaGamePhase.RegularSeason, provenance, [sample]));
        Should.Throw<ArgumentException>(() => new CompletedBoxScore(game, 2026, date, (NbaGamePhase)99, provenance, [sample]));
        var incomplete = new PlayerGameSample(game, sample.PlayerId, 2026, date, false, true, sample.Statistics, provenance);
        Should.Throw<ArgumentException>(() => new CompletedBoxScore(game, 2026, date, NbaGamePhase.RegularSeason, provenance, [incomplete]));
    }
}

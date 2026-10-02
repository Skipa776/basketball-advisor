using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Trends;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Trends;

public sealed class HeatLabelServiceTests
{
    private const string Parameters = """
        {"recentGames":5,"minBaselineGames":10,"maxBaselineGames":30,"minBaselineMinutes":15,"nu":26,
         "tauRelative":0.18,"effectFloorPoints":2,"effectFloorSd":0,"cvByMinutes":[{"minutesFrom":0,"minutesTo":60,"cv":0.4}]}
        """;

    [Fact]
    public async Task HL01_a_role_jump_is_labelled_with_cause_interval_and_disclaimer()
    {
        var store = new Store();
        var riser = new PlayerId(Guid.NewGuid());
        var steady = new PlayerId(Guid.NewGuid());
        store.Samples = [.. Games(riser, 0, 10, 20m, 30m), .. Games(riser, 10, 5, 30m, 45m), .. Games(steady, 0, 15, 20m, 30m)];
        var service = new HeatLabelService(store, store, store, new PointsScoringEngine());

        var page = await service.QueryAsync(
            store.League!.Id, 2026, DataSourceName.Manual, new DateOnly(2026, 4, 12), TestContext.Current.CancellationToken);

        var label = page.Labels.ShouldHaveSingleItem();
        label.PlayerId.ShouldBe(riser.Value);
        label.Label.ShouldBe("HOT");
        label.Cause.ShouldBe("Role");
        label.ShiftLow.ShouldBeLessThan(label.ShiftMean);
        label.ShiftHigh.ShouldBeGreaterThan(label.ShiftMean);
        page.EligiblePlayers.ShouldBe(2);
        page.QualifiedPlayers.ShouldBe(2);
        page.NullLabelRate.ShouldBe(0.25m);
        page.ExpectedChanceLabels.ShouldBe(1);
        page.ModelVersion.ShouldBe("heat-prior-test");
        page.Disclaimer.ShouldBe(
            "Describes the last 5 games against the 10–30 before them. Not a forecast. About 25% of players get a label like this by chance alone.");
    }

    [Fact]
    public async Task HL02_no_active_model_or_a_category_league_is_a_conflict()
    {
        var store = new Store { Model = null };
        var service = new HeatLabelService(store, store, store, new PointsScoringEngine());
        var token = TestContext.Current.CancellationToken;

        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.QueryAsync(store.League!.Id, 2026, DataSourceName.Manual, new DateOnly(2026, 4, 12), token));
        store.League = new FantasyLeague(
            store.League!.Id, "Category", LeagueType.Categories, 7, [], [StatKey.PTS], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.QueryAsync(store.League.Id, 2026, DataSourceName.Manual, new DateOnly(2026, 4, 12), token));
    }

    private static IEnumerable<PlayerGameSample> Games(PlayerId player, int firstDay, int count, decimal points, decimal minutes) =>
        Enumerable.Range(firstDay, count).Select(day => new PlayerGameSample(
            Guid.NewGuid(),
            player,
            2026,
            new DateOnly(2025, 10, 21).AddDays(day),
            true,
            true,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = points,
                [StatKey.MIN] = minutes,
                [StatKey.FGA] = minutes / 2m,
            }),
            new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64))));

    private sealed class Store : ILeagueRepository, IBoxScoreRepository, IModelVersionRepository
    {
        public FantasyLeague? League { get; set; } = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
        public IReadOnlyList<PlayerGameSample> Samples { get; set; } = [];
        public ModelVersion? Model { get; init; } = new(
            HeatPriorParameters.ModelName, "heat-prior-test", DateTimeOffset.UnixEpoch, [2026], Parameters,
            """{"falseLabels":{"nullLabelRate":0.25}}""", string.Empty);

        public Task<FantasyLeague?> GetAsync(Guid id, CancellationToken token) => Task.FromResult(League);
        public Task<IReadOnlyList<PlayerGameSample>> ListAsync(int season, string source, NbaGamePhase phase, DateOnly date, CancellationToken token) =>
            Task.FromResult(Samples);
        public Task<ModelVersion?> GetActiveAsync(string modelName, CancellationToken token) => Task.FromResult(Model);
        public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<FantasyLeague>> ListAsync(CancellationToken token) => throw new NotSupportedException();
        public Task SaveScoringAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task SaveSettingsAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid gameId, string source, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(ModelVersion version, CancellationToken token) => throw new NotSupportedException();
        public Task ActivateAsync(string modelName, string version, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModelVersion>> ListAsync(string modelName, CancellationToken token) => throw new NotSupportedException();
    }
}

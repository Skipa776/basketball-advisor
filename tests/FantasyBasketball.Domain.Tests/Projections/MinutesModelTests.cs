using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Projections;

public sealed class MinutesModelTests
{
    // mu 20, kappa 10, weights 1 / 0.5 / 0.25, shifts bench +2, rotation -1, starter 0, none -4, beta -0.3.
    private static readonly MinutesModel Model = new(Parameters());

    [Fact]
    public void MM01_no_usable_history_is_the_league_mean_plus_the_none_shift_and_age()
    {
        Model.Project(2026, []).ShouldBe(16m);
        Model.FromHistory(null, []).ShouldBe(16m);
        Model.FromHistory(37, []).ShouldBe(16m - 3m);
    }

    [Fact]
    public void MM02_role_comes_from_last_seasons_minutes_and_a_missing_last_season_is_none()
    {
        // 50 games pins the shrinkage at (50 m + 200) / 60, so only the shift differs.
        Model.FromHistory(27, [new MinutesHistory(1, 50, 17.9m, null)]).ShouldBe(((50m * 17.9m) + 200m) / 60m + 2m);
        Model.FromHistory(27, [new MinutesHistory(1, 50, 18m, null)]).ShouldBe(((50m * 18m) + 200m) / 60m - 1m);
        Model.FromHistory(27, [new MinutesHistory(1, 50, 28m, null)]).ShouldBe(((50m * 28m) + 200m) / 60m);
        // No last season: the lag-2 season is the newest played, so it takes weight 1.
        Model.FromHistory(27, [new MinutesHistory(2, 100, 30m, null)]).ShouldBe(((100m * 30m) + 200m) / 110m - 4m);
    }

    [Fact]
    public void MM03_projection_is_clamped_to_zero_and_the_maximum()
    {
        Model.FromHistory(15, [new MinutesHistory(1, 82, 41.9m, null)]).ShouldBe(42m); // 39.52 + 3.6 = 43.1
        Model.FromHistory(60, [new MinutesHistory(1, 82, 1m, null)]).ShouldBe(0m);
    }

    [Fact]
    public void MM04_seasons_are_games_and_recency_weighted_and_short_or_old_ones_dropped()
    {
        var lines = new[]
        {
            Line(2025, 60, 30m, 25), // lag 1, weight 1: 60 games
            Line(2024, 40, 20m, 24), // lag 2, weight 0.5: 20 games
            Line(2023, 4, 40m, 23), // under 5 games: dropped
            Line(2022, 80, 10m, 22), // lag 4: dropped
        };

        // (60*30 + 20*20 + 10*20) / (60 + 20 + 10) = 26.6667, starter? no: 30 >= 28 -> starter (0);
        // age 26 -> -1 * -0.3 = +0.3.
        Model.Project(2026, lines).ShouldBe((2400m / 90m) + 0.3m);
    }

    [Fact]
    public void MM06_recency_counts_from_the_newest_season_played()
    {
        // Sat out last season: lags 2 and 3 take weights 1 and 0.5, as lags 1 and 2 would.
        var absent = Model.FromHistory(27, [new MinutesHistory(2, 70, 34m, null), new MinutesHistory(3, 60, 32m, null)]);
        var present = Model.FromHistory(27, [new MinutesHistory(1, 70, 34m, null), new MinutesHistory(2, 60, 32m, null)]);

        absent.ShouldBe(((70m * 34m) + (30m * 32m) + 200m) / 110m - 4m);
        (present - absent).ShouldBe(4m, "only the role shift differs: none (-4) against starter (0)");
    }

    [Fact]
    public void MM05_parameters_missing_a_role_shift_are_rejected()
    {
        var json = """{"ageCenter":27,"maxMinutes":42,"minHistoryGames":5,"rotationFrom":18,"starterFrom":28,"weights":[1,0.5,0.25],"mu":20,"kappa":10,"delta":{"bench":1},"beta":0,"sigma":30,"tau":1}""";

        Should.Throw<ArgumentException>(() => MinutesModelParameters.Parse(json)).Message.ShouldContain("rotation");
    }

    private static MinutesModelParameters Parameters() => new(
        27, 42m, 5, 18m, 28m, [1m, 0.5m, 0.25m], 20m, 10m,
        new Dictionary<string, decimal> { ["bench"] = 2m, ["rotation"] = -1m, ["starter"] = 0m, ["none"] = -4m },
        -0.3m, 30m, 1m);

    private static SeasonStatLine Line(int season, int games, decimal minutesPerGame, int? age) => new(
        new PlayerId(Guid.NewGuid()),
        season,
        games,
        minutesPerGame,
        new StatLine(new Dictionary<StatKey, decimal>()),
        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.MIN] = games * minutesPerGame }),
        null,
        new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64)),
        age);
}

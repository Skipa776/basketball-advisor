using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Trends;

public sealed class PlayerHeatTests
{
    private static readonly PlayerId Player = new(new Guid(1, 0, 0, new byte[8]));
    private static readonly DateOnly Start = new(2025, 10, 1);
    private static readonly DateOnly Through = Start.AddDays(80);
    private static readonly FantasyLeague League = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
    private static readonly PlayerHeatCalculator Calculator = new(new PointsScoringEngine(), new PlayerHeatOptions());

    [Theory]
    [InlineData(9, 1, 6)]
    [InlineData(10, 1, 7)]
    [InlineData(12, 1, 9)]
    [InlineData(13, 1, 10)]
    [InlineData(30, 1, 27)]
    [InlineData(33, 1, 30)]
    [InlineData(34, 2, 31)]
    [InlineData(40, 8, 37)]
    public void HE01_appearance_boundaries_keep_recent_and_baseline_disjoint(int count, int first, int last)
    {
        var result = Calculate(Enumerable.Range(1, count).Select(index => Game(index, index)).Reverse().ToArray());
        result.SeasonAppearances.ShouldBe(count);
        result.BaselineWindow.Select(game => game.FantasyPoints)
            .ShouldBe(Enumerable.Range(first, last - first + 1).Select(value => (decimal)value));
        result.RecentWindow.Select(game => game.FantasyPoints)
            .ShouldBe(Enumerable.Range(count - 2, 3).Select(value => (decimal)value));
        result.CurrentWindow.Select(game => game.FantasyPoints)
            .ShouldBe(Enumerable.Range(Math.Max(1, count - 29), Math.Min(count, 30)).Select(value => (decimal)value));
        result.BaselineWindow.Select(game => game.GameId)
            .Intersect(result.RecentWindow.Select(game => game.GameId)).ShouldBeEmpty();
        result.HasComparison.ShouldBe(count >= 13);
        if (count < 10) result.CurrentAverage.ShouldBeNull();
        else result.CurrentAverage.ShouldBe(result.CurrentWindow.Average(game => game.FantasyPoints));
        if (count < 13) result.IsAboveBaseline.ShouldBeNull();
        else result.BaselineAverage.ShouldBe((first + last) / 2m);
        result.Policy.RecentGames.ShouldBe(3);
        result.Policy.MinimumBaselineGames.ShouldBe(10);
        result.Policy.MaximumBaselineGames.ShouldBe(30);
        result.ModelVersion.ShouldBe(PlayerHeatOptions.ModelVersion);
    }

    [Fact]
    public void HE02_streak_never_raises_its_own_comparison_but_does_change_current_average()
    {
        var result = Calculate(Series(Player, 20m, 100m));
        result.BaselineAverage.ShouldBe(20m);
        result.RecentAverage.ShouldBe(100m);
        result.PointsAboveBaseline.ShouldBe(80m);
        result.CurrentAverage.ShouldBe(500m / 13m);
        result.RelativeLift.ShouldBe(4m);
        result.RecentGamesAboveBaseline.ShouldBe(3);
        result.IsAboveBaseline.ShouldBe(true);
        result.LatestAppearance.ShouldBe(Start.AddDays(13));
        result.RecentWindow[0].Provenance.Source.ShouldBe(DataSourceName.Manual);
    }

    [Fact]
    public void HE03_dnp_future_incomplete_other_player_and_other_season_do_not_contribute()
    {
        var games = Series(Player, 20m, 30m).Concat(new[]
        {
            Game(14, null, didPlay: false),
            Game(15, 900m, isFinal: false),
            Game(16, 900m, player: new PlayerId(Guid.NewGuid())),
            Game(17, 900m, season: 2025),
            Game(100, 900m),
        }).ToArray();
        var result = Calculate(games);
        result.SeasonAppearances.ShouldBe(13);
        result.RecentAverage.ShouldBe(30m);
        result.BaselineAverage.ShouldBe(20m);
        result.LatestAppearance.ShouldBe(Start.AddDays(13));
        var newSeason = Calculator.Calculate(Player, League, 2027, Through, games);
        newSeason.SeasonAppearances.ShouldBe(0);
        newSeason.CurrentAverage.ShouldBeNull();
        newSeason.PointsAboveBaseline.ShouldBeNull();
        newSeason.LatestAppearance.ShouldBeNull();
    }

    [Fact]
    public void HE04_zero_scoring_appearances_count_and_zero_baseline_has_no_percentage()
    {
        var result = Calculate(Series(Player, 0m, 7m));
        result.SeasonAppearances.ShouldBe(13);
        result.BaselineAverage.ShouldBe(0m);
        result.PointsAboveBaseline.ShouldBe(7m);
        result.RelativeLift.ShouldBeNull();
        result.IsAboveBaseline.ShouldBe(true);
        var unchanged = Calculate(Series(Player, 0m, 0m));
        unchanged.PointsAboveBaseline.ShouldBe(0m);
        unchanged.IsAboveBaseline.ShouldBe(false);
    }

    [Fact]
    public void HE05_negative_baselines_use_signed_point_lift_and_absolute_denominator()
    {
        var result = Calculator.Calculate(Player, NegativeLeague(), 2026, Through, Series(Player, 10m, 5m));
        result.BaselineAverage.ShouldBe(-10m);
        result.RecentAverage.ShouldBe(-5m);
        result.PointsAboveBaseline.ShouldBe(5m);
        result.RelativeLift.ShouldBe(0.5m);
        result.CurrentAverage.ShouldBe(-115m / 13m);
        result.IsAboveBaseline.ShouldBe(true);
        var falling = Calculate(Series(Player, 10m, 5m));
        falling.PointsAboveBaseline.ShouldBe(-5m);
        falling.RelativeLift.ShouldBe(-0.5m);
        falling.IsAboveBaseline.ShouldBe(false);
    }

    [Fact]
    public void HE06_inadequate_samples_never_get_a_zero_heat_score_or_a_cold_label()
    {
        foreach (var count in new[] { 0, 1, 2, 3, 9, 10, 12 })
        {
            var result = Calculate(Enumerable.Range(1, count).Select(index => Game(index, 100m)).ToArray());
            result.PointsAboveBaseline.ShouldBeNull();
            result.RelativeLift.ShouldBeNull();
            result.RecentGamesAboveBaseline.ShouldBeNull();
            result.IsAboveBaseline.ShouldBeNull();
            result.HasComparison.ShouldBeFalse();
            if (count < 3) result.RecentAverage.ShouldBeNull();
        }
    }

    [Fact]
    public void HE07_duplicates_fail_and_input_order_does_not_affect_results()
    {
        var games = Series(Player, 10m, 20m);
        Should.Throw<ArgumentException>(() => Calculate([.. games, games[0]]))
            .Message.ShouldContain("Duplicate");
        var first = Calculate(games);
        var shuffled = Calculate(games.Reverse().ToArray());
        shuffled.BaselineWindow.ShouldBe(first.BaselineWindow);
        shuffled.RecentWindow.ShouldBe(first.RecentWindow);
        shuffled.CurrentAverage.ShouldBe(first.CurrentAverage);
        var tiedDates = new[] { Game(1, 1m, day: 1), Game(2, 2m, day: 1) };
        Calculate(tiedDates.Reverse().ToArray()).CurrentWindow.Select(game => game.FantasyPoints)
            .ShouldBe(new[] { 1m, 2m });
        Should.Throw<NotSupportedException>(() => ((IList<ScoredAppearance>)first.RecentWindow).Clear());
    }

    [Fact]
    public void HE08_best_performing_and_hottest_are_distinct_and_recompute_after_games_or_rules_change()
    {
        var second = new PlayerId(new Guid(2, 0, 0, new byte[8]));
        var third = new PlayerId(new Guid(3, 0, 0, new byte[8]));
        var insufficient = new PlayerId(new Guid(4, 0, 0, new byte[8]));
        var games = Series(Player, 40m, 40m)
            .Concat(Series(second, 10m, 30m))
            .Concat(Series(third, 20m, 25m))
            .Append(Game(1, 1000m, player: insufficient)).ToArray();
        var rankings = Calculator.Rank(League, 2026, Through, games);
        rankings.BestPerforming.Select(result => result.PlayerId).ShouldBe(new[] { Player, third, second });
        rankings.Hottest.Select(result => result.PlayerId).ShouldBe(new[] { second, third });
        var updated = Calculator.Rank(League, 2026, Through,
            [.. games, Game(14, 0m, player: second), Game(15, 0m, player: second), Game(16, 0m, player: second)]);
        updated.Hottest.Select(result => result.PlayerId).ShouldBe(new[] { third });
        var rescored = Calculator.Rank(NegativeLeague(), 2026, Through, games);
        rescored.BestPerforming[0].PlayerId.ShouldBe(second);
        rescored.Hottest.ShouldBeEmpty();
        Calculator.Rank(League, 2027, Through, games).BestPerforming.ShouldBeEmpty();
        var ties = Calculator.Rank(League, 2026, Through, [.. Series(third, 10m, 20m), .. Series(second, 10m, 20m)]);
        ties.Hottest.Select(result => result.PlayerId).ShouldBe(new[] { second, third });
        ties.BestPerforming.Select(result => result.PlayerId).ShouldBe(new[] { second, third });
    }

    [Theory]
    [InlineData(0, 10, 30)]
    [InlineData(3, 0, 30)]
    [InlineData(3, 10, 9)]
    public void HE09_invalid_window_options_are_rejected(int recent, int minimum, int maximum)
    {
        var calculator = new PlayerHeatCalculator(new PointsScoringEngine(), new PlayerHeatOptions
        {
            RecentGames = recent,
            MinimumBaselineGames = minimum,
            MaximumBaselineGames = maximum,
        });
        Should.Throw<ArgumentException>(() => calculator.Calculate(Player, League, 2026, Through, []));
    }

    [Fact]
    public void HE09_effective_policy_is_used_and_category_leagues_are_rejected()
    {
        var policy = new PlayerHeatOptions { RecentGames = 2, MinimumBaselineGames = 4, MaximumBaselineGames = 6 };
        var calculator = new PlayerHeatCalculator(new PointsScoringEngine(), policy);
        var result = calculator.Calculate(Player, League, 2026, Through,
            Enumerable.Range(1, 8).Select(index => Game(index, index)).ToArray());
        result.Policy.ShouldBe(policy);
        result.RecentAverage.ShouldBe(7.5m);
        result.BaselineAverage.ShouldBe(3.5m);
        result.CurrentAverage.ShouldBe(5.5m);
        var categories = new FantasyLeague(Guid.NewGuid(), "Categories", LeagueType.Categories,
            7, [], [StatKey.PTS], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        Should.Throw<ArgumentException>(() => calculator.Calculate(Player, categories, 2026, Through, []));
        Should.Throw<ArgumentOutOfRangeException>(() => Calculate([], season: 1));
        Should.Throw<ArgumentException>(() => calculator.Calculate(default, League, 2026, Through, []));
    }

    [Fact]
    public void HE10_final_appearance_requires_stats_dnp_is_explicit_and_provenance_is_required()
    {
        Should.Throw<ArgumentException>(() => Game(1, null));
        Should.Throw<ArgumentException>(() => Game(1, 0m, didPlay: false));
        Should.Throw<ArgumentException>(() => new PlayerGameSample(Guid.Empty, Player, 2026, Start, true, false, null, Provenance()));
        Should.Throw<ArgumentException>(() => new PlayerGameSample(Guid.NewGuid(), default, 2026, Start, true, false, null, Provenance()));
        Should.Throw<ArgumentOutOfRangeException>(() => Game(1, 1m, season: 1));
        Should.Throw<ArgumentNullException>(() => new PlayerGameSample(Guid.NewGuid(), Player, 2026, Start, true, false, null, null!));
        Game(1, null, isFinal: false).Statistics.ShouldBeNull();
        Game(1, null, didPlay: false).DidPlay.ShouldBeFalse();
    }

    private static PlayerHeatResult Calculate(IReadOnlyList<PlayerGameSample> games, int season = 2026) =>
        Calculator.Calculate(Player, League, season, Through, games);

    private static PlayerGameSample[] Series(PlayerId player, decimal baseline, decimal recent) =>
        Enumerable.Range(1, 13).Select(index => Game(index, index <= 10 ? baseline : recent, player: player)).ToArray();

    private static PlayerGameSample Game(int index, decimal? points, bool isFinal = true, bool didPlay = true,
        PlayerId? player = null, int season = 2026, int? day = null) =>
        new(new Guid(index, 0, 0, new byte[8]), player ?? Player, season,
            Start.AddDays(day ?? index), isFinal, didPlay,
            points is { } value ? new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = value }) : null,
            Provenance());

    private static DataProvenance Provenance() => new(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch,
        null, $"{DataSourceName.Manual}-v1", DataSourceConfidence.ManualEntry, new string('a', 64));

    private static FantasyLeague NegativeLeague() => new(League.Id, "Negative scoring", LeagueType.Points,
        League.TeamCount, [new ScoringRule(StatKey.PTS, -1m)], [], League.RosterSlots, League.Cadence);
}

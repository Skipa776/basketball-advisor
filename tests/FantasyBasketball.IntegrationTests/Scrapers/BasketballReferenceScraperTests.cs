using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Scrapers.BasketballReference;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Scrapers;

public sealed class BasketballReferenceScraperTests
{
    [Fact]
    public void S10_url_builder_allows_season_pages_and_rejects_gamelogs()
    {
        var builder = new BbrefUrlBuilder();

        builder.CreateSeasonUri(2026, BasketballReferenceSeasonPage.PerGame)
            .AbsoluteUri.ShouldBe(
                "https://www.basketball-reference.com/leagues/NBA_2026_per_game.html");
        Should.Throw<InvalidOperationException>(() =>
            builder.CreateUri("/players/j/jokicni01/gamelog/2026"));
        Should.Throw<InvalidOperationException>(() =>
            builder.CreateUri("https://example.com/leagues/NBA_2026_totals.html"));
        Should.Throw<InvalidOperationException>(() =>
            builder.CreateUri(
                "https://www.basketball-reference.com:8443/leagues/NBA_2026_totals.html"));
        Should.Throw<InvalidOperationException>(() =>
            builder.CreateUri(
                "https://agent@www.basketball-reference.com/leagues/NBA_2026_totals.html"));
    }

    [Fact]
    public void S10_box_score_builder_allows_only_dated_game_pages()
    {
        var builder = new BbrefUrlBuilder();

        builder.CreateBoxScoreUri(new DateOnly(2025, 11, 16), "BOS")
            .AbsoluteUri.ShouldBe(
                "https://www.basketball-reference.com/boxscores/202511160BOS.html");
        foreach (var path in new[]
        {
            "/boxscores/?month=11&day=16&year=2025",
            "/boxscores/202511160BOS.html?x=1",
            "/boxscores/202511160bos.html",
            "/boxscores/pbp/202511160BOS.html",
            "/boxscores/shot-chart/202511160BOS.html",
            "/players/h/hardeja01/gamelog/2026",
        })
        {
            Should.Throw<InvalidOperationException>(() => builder.CreateUri(path));
        }
    }

    [Fact]
    public void Team_codes_map_between_balldontlie_and_basketball_reference()
    {
        BbrefTeamCodes.FromBallDontLie("BKN").ShouldBe("BRK");
        BbrefTeamCodes.FromBallDontLie("CHA").ShouldBe("CHO");
        BbrefTeamCodes.FromBallDontLie("PHX").ShouldBe("PHO");
        BbrefTeamCodes.FromBallDontLie("BOS").ShouldBe("BOS");
        BbrefTeamCodes.ToBallDontLie("BRK").ShouldBe("BKN");
        BbrefTeamCodes.ToBallDontLie("PHO").ShouldBe("PHX");
        BbrefTeamCodes.ToBallDontLie("LAC").ShouldBe("LAC");
    }

    [Fact]
    public async Task I04_I05_and_S13_three_tables_parse_purely()
    {
        var parser = new SeasonTableParser();
        var perGame = await ReadFixtureAsync(
            "basketball-reference-per-game-2026-07-29.html");
        var totals = await ReadFixtureAsync(
            "basketball-reference-totals-2026-07-29.html");
        var advanced = await ReadFixtureAsync(
            "basketball-reference-advanced-2026-07-29.html");

        var first = parser.Parse(perGame, totals, advanced);
        var second = parser.Parse(perGame, totals, advanced);

        first.Count.ShouldBe(1);
        second.Count.ShouldBe(1);
        var player = first[0];
        var repeatedPlayer = second[0];
        player.ExternalPlayerId.ShouldBe(repeatedPlayer.ExternalPlayerId);
        player.PlayerName.ShouldBe(repeatedPlayer.PlayerName);
        player.GamesPlayed.ShouldBe(repeatedPlayer.GamesPlayed);
        player.MinutesPerGame.ShouldBe(repeatedPlayer.MinutesPerGame);
        player.PerGame.Values.ShouldBe(repeatedPlayer.PerGame.Values);
        player.Totals.Values.ShouldBe(repeatedPlayer.Totals.Values);
        player.UsageRate.ShouldBe(repeatedPlayer.UsageRate);
        player.RawFragment.ShouldBe(repeatedPlayer.RawFragment);
        player.ExternalPlayerId.ShouldBe("jokicni01");
        player.GamesPlayed.ShouldBe(72);
        player.MinutesPerGame.ShouldBe(34.5m);
        player.PerGame[StatKey.PTS].ShouldBe(27.1m);
        player.PerGame[StatKey.REB].ShouldBe(
            player.PerGame[StatKey.OREB] + player.PerGame[StatKey.DREB]);
        player.Totals[StatKey.REB].ShouldBe(
            player.Totals[StatKey.OREB] + player.Totals[StatKey.DREB]);
        player.UsageRate.ShouldBe(0.245m);
    }

    [Fact]
    public async Task S14_missing_expected_column_fails_loudly()
    {
        var parser = new SeasonTableParser();
        var perGame = (await ReadFixtureAsync(
            "basketball-reference-per-game-2026-07-29.html"))
            .Replace("<th>TRB</th>", "<th>REBOUNDS</th>", StringComparison.Ordinal);
        var totals = await ReadFixtureAsync(
            "basketball-reference-totals-2026-07-29.html");
        var advanced = await ReadFixtureAsync(
            "basketball-reference-advanced-2026-07-29.html");

        var exception = Should.Throw<InvalidDataException>(() => parser.Parse(
            perGame,
            totals,
            advanced));

        exception.Message.ShouldContain("TRB");
    }

    private static Task<string> ReadFixtureAsync(string name) =>
        File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Html", name),
            TestContext.Current.CancellationToken);
}

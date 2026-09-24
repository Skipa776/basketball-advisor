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

    // Live 2025-26 season pages (verified 2026-09-23) end the table body with a
    // league summary row, list traded players once per team plus an "nTM" row, and
    // round per-game rebounds independently. The fixtures are edited to match.
    [Fact]
    public async Task S13_league_average_row_is_skipped_but_other_unidentified_rows_fail()
    {
        var (perGame, totals, advanced) = await SeasonFixturesAsync();
        var summary = perGame.Replace("</tbody>", "<tr><td>League Average</td><td></td></tr></tbody>", StringComparison.Ordinal);
        new SeasonTableParser().Parse(summary, totals, advanced).ShouldHaveSingleItem().ExternalPlayerId.ShouldBe("jokicni01");

        var unknown = perGame.Replace("</tbody>", "<tr><td>Mystery Row</td><td></td></tr></tbody>", StringComparison.Ordinal);
        Should.Throw<InvalidDataException>(() => new SeasonTableParser().Parse(unknown, totals, advanced));
    }

    [Fact]
    public async Task S13_traded_player_uses_the_combined_team_row()
    {
        var (perGame, totals, advanced) = await SeasonFixturesAsync();
        var parser = new SeasonTableParser();

        var traded = parser.Parse(WithTeams(perGame, "2TM", "DEN", "LAL"), WithTeams(totals, "2TM", "DEN", "LAL"), advanced);
        traded.ShouldHaveSingleItem().PerGame[StatKey.PTS].ShouldBe(27.1m, "the per-team copies score 99.9");

        Should.Throw<InvalidDataException>(() => parser.Parse(WithTeams(perGame, "DEN", "LAL"), totals, advanced))
            .Message.ShouldContain("without one combined-team row");
    }

    [Fact]
    public async Task S13_per_game_rebounds_allow_rounding_but_totals_must_add_up()
    {
        var (perGame, totals, advanced) = await SeasonFixturesAsync();
        var parser = new SeasonTableParser();

        // ORB 3.1 + DRB 9.2 = 12.3: 12.4 is rounding, 12.5 is wrong.
        parser.Parse(perGame.Replace("<td>12.3</td>", "<td>12.4</td>", StringComparison.Ordinal), totals, advanced)
            .ShouldHaveSingleItem();
        Should.Throw<InvalidDataException>(() =>
            parser.Parse(perGame.Replace("<td>12.3</td>", "<td>12.5</td>", StringComparison.Ordinal), totals, advanced));
        Should.Throw<InvalidDataException>(() =>
            parser.Parse(perGame, totals.Replace("<td>885.6</td>", "<td>885.7</td>", StringComparison.Ordinal), advanced));
    }

    private static async Task<(string PerGame, string Totals, string Advanced)> SeasonFixturesAsync() =>
        (await ReadFixtureAsync("basketball-reference-per-game-2026-07-29.html"),
            await ReadFixtureAsync("basketball-reference-totals-2026-07-29.html"),
            await ReadFixtureAsync("basketball-reference-advanced-2026-07-29.html"));

    // Adds a Team column and repeats the single player row once per team. Rows after the
    // first score 99.9 points so a wrongly chosen row is visible.
    private static string WithTeams(string html, params string[] teams)
    {
        var start = html.IndexOf("<tbody>", StringComparison.Ordinal) + "<tbody>".Length;
        var end = html.IndexOf("</tbody>", StringComparison.Ordinal);
        var row = html[start..end];
        var firstCellEnd = row.IndexOf("</td>", StringComparison.Ordinal) + "</td>".Length;
        var rows = teams.Select((team, index) =>
        {
            var withTeam = row[..firstCellEnd] + $"<td>{team}</td>" + row[firstCellEnd..];
            return index == 0 ? withTeam : withTeam.Replace("<td>27.1</td>", "<td>99.9</td>", StringComparison.Ordinal);
        });
        return html[..start].Replace("<th>Player</th>", "<th>Player</th><th>Team</th>", StringComparison.Ordinal)
            + string.Concat(rows) + html[end..];
    }

    private static Task<string> ReadFixtureAsync(string name) =>
        File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Html", name),
            TestContext.Current.CancellationToken);
}

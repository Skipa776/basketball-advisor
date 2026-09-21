using System.Security.Cryptography;
using System.Text;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Scrapers.BasketballReference;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Scrapers;

public sealed class BoxScoreParserTests
{
    private const string GameId = "202601020AAA";
    private static string Fixture() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures/Html/basketball-reference-boxscore-synthetic-2026-09-20.html"));

    [Fact]
    public void BS01_S32_full_game_and_comment_tables_parse_deterministically()
    {
        var html = Fixture();
        var parser = new BoxScoreParser();
        var first = parser.Parse(html, GameId);
        var second = parser.Parse(html, GameId);
        first.Players.Count.ShouldBe(3);
        first.PlayedOn.ShouldBe(new DateOnly(2026, 1, 2));
        first.RawRecordHash.ShouldBe(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html))).ToLowerInvariant());
        second.RawRecordHash.ShouldBe(first.RawRecordHash);
        second.Players.Select(player => player.ExternalPlayerId).ShouldBe(first.Players.Select(player => player.ExternalPlayerId));
        var line = first.Players[0].Statistics!;
        line[StatKey.MIN].ShouldBe(30.5m);
        line[StatKey.PTS].ShouldBe(10m);
        line[StatKey.REB].ShouldBe(line[StatKey.OREB] + line[StatKey.DREB]);
        line[StatKey.FG_PCT].ShouldBe(0.5m);
        line[StatKey.FT_PCT].ShouldBe(0.5m);
        line[StatKey.FG3_PCT].ShouldBe(1m / 3m);
        parser.Parse(html + " ", GameId).RawRecordHash.ShouldNotBe(first.RawRecordHash);
        var visible = html.Replace("<!--<table", "<table").Replace("</table>-->", "</table>");
        parser.Parse(visible, GameId).Players[2].Statistics!.Values.ShouldBe(first.Players[2].Statistics!.Values);
    }

    [Fact]
    public void BS02_dnp_is_distinct_from_a_played_zero()
    {
        var players = new BoxScoreParser().Parse(Fixture(), GameId).Players;
        players[1].DidPlay.ShouldBeFalse();
        players[1].Statistics.ShouldBeNull();
        players[2].DidPlay.ShouldBeTrue();
        players[2].Statistics.ShouldNotBeNull();
        players[2].Statistics![StatKey.MIN].ShouldBe(1m / 60m);
        players[2].Statistics![StatKey.PTS].ShouldBe(0m);
        Should.Throw<InvalidDataException>(() => new BoxScoreParser().Parse(Fixture().Replace("Did Not Play", "Status unknown"), GameId));
    }

    [Theory]
    [InlineData("data-stat=\"ast\"", "data-stat=\"renamed\"")]
    [InlineData("<td data-stat=\"ast\">5</td>", "")]
    [InlineData("<td data-stat=\"ast\">5</td>", "<td data-stat=\"ast\">5</td><td data-stat=\"ast\">5</td>")]
    public void BS03_missing_or_duplicate_required_columns_fail_loudly(string original, string replacement) =>
        Should.Throw<InvalidDataException>(() => new BoxScoreParser().Parse(Fixture().Replace(original, replacement), GameId));

    [Theory]
    [InlineData("30:30", "30:60")]
    [InlineData("30:30", "-1:20")]
    [InlineData("30:30", "30.5")]
    [InlineData("data-stat=\"pts\">10", "data-stat=\"pts\">-1")]
    [InlineData("data-stat=\"pts\">10", "data-stat=\"pts\">1.5")]
    [InlineData("data-stat=\"trb\">7", "data-stat=\"trb\">9")]
    [InlineData("data-stat=\"fg\">4", "data-stat=\"fg\">9")]
    [InlineData("data-stat=\"ft\">1", "data-stat=\"ft\">3")]
    [InlineData("data-stat=\"fg3\">1", "data-stat=\"fg3\">4")]
    [InlineData("data-stat=\"fg3a\">3", "data-stat=\"fg3a\">9")]
    public void BS04_invalid_stat_values_never_become_zeros(string original, string replacement) =>
        Should.Throw<InvalidDataException>(() => new BoxScoreParser().Parse(Fixture().Replace(original, replacement), GameId));

    [Theory]
    [InlineData("fixturebeta01", "fixturealpha01")]
    [InlineData("box-BBB-game-basic", "box-AAA-game-basic")]
    [InlineData("box-BBB-game-basic", "box-BBB-q1-basic")]
    [InlineData("/boxscores/202601020AAA.html", "/boxscores/202601030AAA.html")]
    [InlineData("www.basketball-reference.com", "example.com")]
    [InlineData("data-append-csv=\"fixturealpha01\"", "data-append-csv=\"wrongplayer01\"")]
    public void BS05_wrong_game_or_ambiguous_identity_is_rejected(string original, string replacement) =>
        Should.Throw<InvalidDataException>(() => new BoxScoreParser().Parse(Fixture().Replace(original, replacement), GameId));
}

using FantasyBasketball.Application.Leagues;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Leagues;

public sealed class RosterCsvTests
{
    [Fact]
    public void Parses_header_columns_in_any_order_quoted_fields_and_mine_flags()
    {
        var rows = RosterCsv.Parse("Player,Mine,Team\r\n\"Jokić, Nikola\",yes,\"The \"\"Joker\"\" Squad\"\n\nLuka Doncic,,Rivals\n");

        rows.Count.ShouldBe(2);
        rows[0].ShouldBe(new RosterCsvRow(2, "The \"Joker\" Squad", "Jokić, Nikola", true));
        rows[1].ShouldBe(new RosterCsvRow(4, "Rivals", "Luka Doncic", false));
        RosterCsv.Parse(RosterCsv.Template).Count(row => row.Mine).ShouldBe(2);
    }

    [Theory]
    [InlineData("", "at most")]
    [InlineData("Name,Club\nA,B", "header with Team and Player")]
    [InlineData("Team,Player\n", "no roster rows")]
    [InlineData("Team,Player\nSquad,", "Line 2 needs both")]
    [InlineData("Team,Player\n\"Squad,Jokic", "Line 2 has an unclosed quote")]
    public void Malformed_csv_fails_with_a_named_reason(string csv, string reason) =>
        Should.Throw<FormatException>(() => RosterCsv.Parse(csv)).Message.ShouldContain(reason);

    [Fact]
    public void Oversized_input_is_rejected_before_parsing() =>
        Should.Throw<FormatException>(() => RosterCsv.Parse("Team,Player\n" + new string('x', RosterCsv.MaximumLength)));
}

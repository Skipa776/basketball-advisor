using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Players;

public sealed class PlayerAvailabilityTests
{
    [Theory]
    [InlineData("Out", AvailabilityStatus.Out)]
    [InlineData(" ir ", AvailabilityStatus.InjuredReserve)]
    [InlineData("SUS", AvailabilityStatus.Suspended)]
    [InlineData("GTD", AvailabilityStatus.Questionable)]
    [InlineData("DTD", AvailabilityStatus.DayToDay)]
    [InlineData("NA", AvailabilityStatus.Other)]
    public void Provider_codes_map_to_a_status(string code, AvailabilityStatus expected) =>
        PlayerAvailability.ParseStatus(code).ShouldBe(expected);

    [Fact]
    public void Only_out_ir_and_suspended_miss_games_and_the_label_names_the_body_part()
    {
        PlayerAvailability.ParseStatus(" ").ShouldBeNull();
        Report(AvailabilityStatus.InjuredReserve, "Knee").ShouldSatisfyAllConditions(
            report => report.MissesGames.ShouldBeTrue(),
            report => report.Label.ShouldBe("IR (Knee)"));
        Report(AvailabilityStatus.DayToDay, null).ShouldSatisfyAllConditions(
            report => report.MissesGames.ShouldBeFalse(),
            report => report.Label.ShouldBe("Day-to-day"));
    }

    private static PlayerAvailability Report(AvailabilityStatus status, string? bodyPart) =>
        new(new PlayerId(Guid.NewGuid()), status, bodyPart, null, null,
            new DataProvenance(DataSourceName.Sleeper, "1", DateTimeOffset.UnixEpoch, null, "sleeper-v1", DataSourceConfidence.OfficialApi, new string('a', 64)));
}

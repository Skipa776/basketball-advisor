using FantasyBasketball.Domain.Modeling;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Modeling;

public sealed class ModelVersionTests
{
    [Fact]
    public void MV05_rejects_blank_name_and_version()
    {
        Should.Throw<ArgumentException>(() => new ModelVersion(
            " ",
            "v1",
            DateTimeOffset.UnixEpoch,
            [2025],
            "{}",
            "{}",
            string.Empty));
        Should.Throw<ArgumentException>(() => new ModelVersion(
            "minutes-ridge",
            "",
            DateTimeOffset.UnixEpoch,
            [2025],
            "{}",
            "{}",
            string.Empty));
    }

    [Fact]
    public void MV06_requires_non_empty_training_seasons()
    {
        Should.Throw<ArgumentException>(() => new ModelVersion(
            "minutes-ridge",
            "v1",
            DateTimeOffset.UnixEpoch,
            [],
            "{}",
            "{}",
            string.Empty));
    }

    [Fact]
    public void MV07_rejects_training_seasons_before_1947()
    {
        Should.Throw<ArgumentException>(() => new ModelVersion(
            "minutes-ridge",
            "v1",
            DateTimeOffset.UnixEpoch,
            [1900],
            "{}",
            "{}",
            string.Empty));
    }

    [Fact]
    public void MV08_rejects_blank_parameter_and_metric_json()
    {
        Should.Throw<ArgumentException>(() => new ModelVersion(
            "minutes-ridge",
            "v1",
            DateTimeOffset.UnixEpoch,
            [1947],
            " ",
            "{}",
            "card"));
        Should.Throw<ArgumentException>(() => new ModelVersion(
            "minutes-ridge",
            "v1",
            DateTimeOffset.UnixEpoch,
            [1947],
            "{}",
            null!,
            "card"));
    }

    [Fact]
    public void MV09_accepts_the_first_nba_season_and_keeps_every_field()
    {
        var version = new ModelVersion(
            "minutes-ridge",
            "v1",
            DateTimeOffset.UnixEpoch,
            [1947, 2025],
            "{\"a\":1}",
            "{\"b\":2}",
            "card");
        version.ModelName.ShouldBe("minutes-ridge");
        version.TrainSeasonEndYears.ShouldBe([1947, 2025]);
        version.CardMarkdown.ShouldBe("card");
    }
}

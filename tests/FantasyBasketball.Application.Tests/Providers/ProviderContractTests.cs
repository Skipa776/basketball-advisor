using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Providers;

public sealed class ProviderContractTests
{
    [Fact]
    public void External_team_is_valid_by_construction()
    {
        var provenance = new DataProvenance(
            DataSourceName.BallDontLie,
            "1",
            DateTimeOffset.UnixEpoch,
            null,
            "balldontlie-v1",
            DataSourceConfidence.OfficialApi,
            new string('a', 64));
        var team = new ExternalTeam("1", " Atlanta Hawks ", " atl ", provenance);

        team.ExternalId.ShouldBe("1");
        team.Name.ShouldBe("Atlanta Hawks");
        team.Abbreviation.ShouldBe("ATL");
        team.Provenance.ShouldBe(provenance);

        Should.Throw<ArgumentException>(() =>
            new ExternalTeam("", "Atlanta Hawks", "ATL", provenance));
        Should.Throw<ArgumentException>(() =>
            new ExternalTeam("1", "", "ATL", provenance));
        Should.Throw<ArgumentException>(() =>
            new ExternalTeam("1", "Atlanta Hawks", "", provenance));
    }
}

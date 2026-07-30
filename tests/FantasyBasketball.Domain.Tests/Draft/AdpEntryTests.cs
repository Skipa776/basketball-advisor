using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Draft;

public sealed class AdpEntryTests
{
    [Fact]
    public void Canonical_adp_requires_positive_position_and_valid_variance()
    {
        var provenance = new DataProvenance(
            DataSourceName.Manual,
            "player",
            DateTimeOffset.UnixEpoch,
            null,
            "manual-v1",
            DataSourceConfidence.ManualEntry,
            new string('a', 64));

        Should.Throw<ArgumentOutOfRangeException>(() => new AdpEntry(
            Guid.NewGuid(),
            new PlayerId(Guid.NewGuid()),
            0m,
            null,
            provenance));
        Should.Throw<ArgumentOutOfRangeException>(() => new AdpEntry(
            Guid.NewGuid(),
            new PlayerId(Guid.NewGuid()),
            1m,
            -0.1m,
            provenance));
    }
}

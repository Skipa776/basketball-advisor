using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Domain.Draft;

public sealed record AdpEntry
{
    public AdpEntry(
        Guid id,
        PlayerId playerId,
        decimal averageDraftPosition,
        decimal? standardDeviation,
        DataProvenance provenance)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("ADP entry id cannot be empty.", nameof(id));
        }

        if (averageDraftPosition <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(averageDraftPosition));
        }

        if (standardDeviation < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardDeviation));
        }

        Id = id;
        PlayerId = playerId;
        AverageDraftPosition = averageDraftPosition;
        StandardDeviation = standardDeviation;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public Guid Id { get; }

    public PlayerId PlayerId { get; }

    public decimal AverageDraftPosition { get; }

    public decimal? StandardDeviation { get; }

    public DataProvenance Provenance { get; }
}

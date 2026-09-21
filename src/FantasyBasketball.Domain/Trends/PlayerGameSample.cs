using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Trends;

/// <summary>A provenance-carrying game observation, never a projection.</summary>
public sealed record PlayerGameSample
{
    public PlayerGameSample(
        Guid gameId,
        PlayerId playerId,
        int seasonEndYear,
        DateOnly playedOn,
        bool isFinal,
        bool didPlay,
        StatLine? statistics,
        DataProvenance provenance)
    {
        if (gameId == Guid.Empty || playerId.Value == Guid.Empty)
        {
            throw new ArgumentException("Game and player IDs are required.");
        }

        if (seasonEndYear < 1947)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonEndYear));
        }

        if (isFinal && didPlay && statistics is null)
        {
            throw new ArgumentException("A completed appearance requires statistics.", nameof(statistics));
        }

        if (!didPlay && statistics is not null)
        {
            throw new ArgumentException("A DNP must not carry a zero-scored stat line.", nameof(statistics));
        }

        ArgumentNullException.ThrowIfNull(provenance);
        GameId = gameId;
        PlayerId = playerId;
        SeasonEndYear = seasonEndYear;
        PlayedOn = playedOn;
        IsFinal = isFinal;
        DidPlay = didPlay;
        Statistics = statistics;
        Provenance = provenance;
    }

    public Guid GameId { get; }
    public PlayerId PlayerId { get; }
    public int SeasonEndYear { get; }
    public DateOnly PlayedOn { get; }
    public bool IsFinal { get; }
    public bool DidPlay { get; }
    public StatLine? Statistics { get; }
    public DataProvenance Provenance { get; }
}

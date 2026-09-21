using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Domain.Trends;

public enum NbaGamePhase { Unknown, RegularSeason, Playoffs, Preseason }

public sealed record CompletedBoxScore
{
    public CompletedBoxScore(Guid gameId, int seasonEndYear, DateOnly playedOn,
        NbaGamePhase phase, DataProvenance provenance, IReadOnlyList<PlayerGameSample> samples)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(samples);
        if (gameId == Guid.Empty || seasonEndYear < 1947 || samples.Count == 0 || !Enum.IsDefined(phase))
        {
            throw new ArgumentException("A completed box score requires a game, season and player observations.");
        }

        if (samples.Any(sample => sample is null || !sample.IsFinal || sample.GameId != gameId
            || sample.SeasonEndYear != seasonEndYear || sample.PlayedOn != playedOn
            || sample.Provenance.Source != provenance.Source)
            || samples.Select(sample => sample.PlayerId).Distinct().Count() != samples.Count)
        {
            throw new ArgumentException("Box-score rows must be distinct final observations from the same game, season, date and source.", nameof(samples));
        }

        GameId = gameId;
        SeasonEndYear = seasonEndYear;
        PlayedOn = playedOn;
        Phase = phase;
        Provenance = provenance;
        Samples = Array.AsReadOnly(samples.ToArray());
    }

    public Guid GameId { get; }
    public int SeasonEndYear { get; }
    public DateOnly PlayedOn { get; }
    public NbaGamePhase Phase { get; }
    public DataProvenance Provenance { get; }
    public ReadOnlyCollection<PlayerGameSample> Samples { get; }
}

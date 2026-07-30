using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Domain.Schedule;

public sealed record NbaGame
{
    public NbaGame(
        Guid id,
        int seasonEndYear,
        DateTimeOffset startsAt,
        NbaTeamId homeTeamId,
        NbaTeamId awayTeamId,
        int? homeScore,
        int? awayScore,
        string status,
        DataProvenance provenance)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("NbaGame id cannot be empty.", nameof(id));
        }

        if (seasonEndYear < 1947)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonEndYear));
        }

        if (startsAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("StartsAt must be UTC.", nameof(startsAt));
        }

        if (homeTeamId == awayTeamId)
        {
            throw new ArgumentException("A team cannot play itself.", nameof(awayTeamId));
        }

        if (homeScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(homeScore));
        }

        if (awayScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(awayScore));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentNullException.ThrowIfNull(provenance);

        Id = id;
        SeasonEndYear = seasonEndYear;
        StartsAt = startsAt;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        HomeScore = homeScore;
        AwayScore = awayScore;
        Status = status.Trim();
        Provenance = provenance;
    }

    public Guid Id { get; }

    public int SeasonEndYear { get; }

    public DateTimeOffset StartsAt { get; }

    public NbaTeamId HomeTeamId { get; }

    public NbaTeamId AwayTeamId { get; }

    public int? HomeScore { get; }

    public int? AwayScore { get; }

    public string Status { get; }

    public DataProvenance Provenance { get; }
}

using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Domain.Stats;

public sealed record SeasonStatLine
{
    public SeasonStatLine(
        PlayerId playerId,
        int seasonEndYear,
        int gamesPlayed,
        decimal minutesPerGame,
        StatLine perGame,
        StatLine totals,
        decimal? usageRate,
        DataProvenance provenance,
        int? age = null)
    {
        if (seasonEndYear < 1947)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonEndYear));
        }

        if (gamesPlayed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamesPlayed));
        }

        if (minutesPerGame < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minutesPerGame));
        }

        if (usageRate is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(usageRate));
        }

        if (age is < 15 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }

        PlayerId = playerId;
        SeasonEndYear = seasonEndYear;
        GamesPlayed = gamesPlayed;
        MinutesPerGame = minutesPerGame;
        PerGame = perGame ?? throw new ArgumentNullException(nameof(perGame));
        Totals = totals ?? throw new ArgumentNullException(nameof(totals));
        UsageRate = usageRate;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        Age = age;
    }

    public PlayerId PlayerId { get; }

    public int SeasonEndYear { get; }

    public int GamesPlayed { get; }

    public decimal MinutesPerGame { get; }

    public StatLine PerGame { get; }

    public StatLine Totals { get; }

    public decimal? UsageRate { get; }

    public DataProvenance Provenance { get; }

    /// <summary>Age on February 1 of the season, as Basketball-Reference defines it.</summary>
    public int? Age { get; }
}

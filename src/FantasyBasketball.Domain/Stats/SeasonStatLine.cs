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
        DataProvenance provenance)
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

        PlayerId = playerId;
        SeasonEndYear = seasonEndYear;
        GamesPlayed = gamesPlayed;
        MinutesPerGame = minutesPerGame;
        PerGame = perGame ?? throw new ArgumentNullException(nameof(perGame));
        Totals = totals ?? throw new ArgumentNullException(nameof(totals));
        UsageRate = usageRate;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
    }

    public PlayerId PlayerId { get; }

    public int SeasonEndYear { get; }

    public int GamesPlayed { get; }

    public decimal MinutesPerGame { get; }

    public StatLine PerGame { get; }

    public StatLine Totals { get; }

    public decimal? UsageRate { get; }

    public DataProvenance Provenance { get; }
}

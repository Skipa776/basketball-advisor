namespace FantasyBasketball.Domain.Projections;

public sealed class MinutesProjector
{
    public decimal Project(
        int gamesPlayed,
        decimal priorMinutesPerGame,
        ProjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsValid())
        {
            throw new ArgumentException("Projection options are invalid.", nameof(options));
        }

        if (gamesPlayed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gamesPlayed));
        }

        if (priorMinutesPerGame is < 0m or > 42m)
        {
            throw new ArgumentOutOfRangeException(nameof(priorMinutesPerGame));
        }

        if (gamesPlayed >= options.FullGamesThreshold)
        {
            return priorMinutesPerGame;
        }

        var playerWeight = (decimal)gamesPlayed / options.FullGamesThreshold;
        return (playerWeight * priorMinutesPerGame)
            + ((1m - playerWeight) * options.MinutesPrior);
    }
}

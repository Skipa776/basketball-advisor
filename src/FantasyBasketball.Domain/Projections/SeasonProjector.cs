using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

/// <summary>A player's per-game projection, its minutes, and the full distribution when every model is present.</summary>
public sealed record SeasonProjection(StatLine PerMinute, StatLine PerGame, decimal Minutes, ProjectionDistribution? Distribution);

/// <summary>
/// Assembles the M2 models into one projection: hierarchical rates × minutes (the minutes
/// model, or a fallback the caller supplies), and — when the minutes, availability and
/// covariance models are all active — a <see cref="ProjectionDistribution"/>.
/// </summary>
public sealed class SeasonProjector(
    ProjectionRateParameters rates,
    MinutesModelParameters? minutes,
    AvailabilityModelParameters? availability,
    StatCovarianceParameters? covariance,
    string modelVersion)
{
    private readonly HierarchicalProjector rateProjector = new(rates);
    private readonly MinutesModel? minutesModel = minutes is null ? null : new MinutesModel(minutes);
    private readonly AvailabilityModel? availabilityModel = availability is null ? null : new AvailabilityModel(availability);
    private readonly StatCovariance? statCovariance = covariance is null || minutes is null ? null : new StatCovariance(covariance, rates, minutes);

    public string ModelVersion => modelVersion;

    public bool HasMinutesModel => minutesModel is not null;

    /// <param name="lines">The player's lines from the three seasons before the target.</param>
    /// <param name="seasonGames">Each history season's length; see <see cref="SeasonLengths"/>.</param>
    /// <param name="fallbackMinutes">Used when no minutes model is active.</param>
    public SeasonProjection Project(
        PlayerId playerId,
        int targetSeasonEndYear,
        string? position,
        IReadOnlyList<SeasonStatLine> lines,
        IReadOnlyDictionary<int, int> seasonGames,
        decimal fallbackMinutes)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var perMinute = rateProjector.ProjectRates(targetSeasonEndYear, position, lines);
        var projectedMinutes = minutesModel?.Project(targetSeasonEndYear, lines) ?? fallbackMinutes;
        var perGame = perMinute.Values.ToDictionary(pair => pair.Key, pair => pair.Value * projectedMinutes);
        perGame[StatKey.MIN] = projectedMinutes;
        var mean = new StatLine(perGame);
        if (availabilityModel is null || statCovariance is null)
        {
            return new SeasonProjection(perMinute, mean, projectedMinutes, null);
        }

        var games = availabilityModel.Project(targetSeasonEndYear, lines, seasonGames);
        var covarianceMatrix = statCovariance.PerGame(position, perMinute, projectedMinutes, games.Mean);
        return new SeasonProjection(perMinute, mean, projectedMinutes,
            new ProjectionDistribution(playerId, mean, covarianceMatrix, games, modelVersion));
    }

    /// <summary>A season's length: the most games anyone logged, capped at a full season.</summary>
    public static IReadOnlyDictionary<int, int> SeasonLengths(IEnumerable<SeasonStatLine> lines, int fullSeason = 82) =>
        lines.GroupBy(line => line.SeasonEndYear)
            .ToDictionary(group => group.Key, group => Math.Min(group.Max(line => line.GamesPlayed), fullSeason));
}

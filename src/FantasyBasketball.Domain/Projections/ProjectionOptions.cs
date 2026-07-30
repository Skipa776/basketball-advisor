namespace FantasyBasketball.Domain.Projections;

public sealed class ProjectionOptions
{
    public const string SectionName = "Projection";

    public decimal RateShrinkageMinutes { get; init; } = 500m;

    public int LeagueAverageMinimumGames { get; init; } = 20;

    public int FullGamesThreshold { get; init; } = 30;

    public decimal MinutesPrior { get; init; } = 20m;

    public decimal GamesPriorWeight { get; init; } = 0.5m;

    public decimal Durability { get; init; } = 0.85m;

    public int SeasonGames { get; init; } = 82;

    public string ModelVersion { get; init; } = "baseline-v1";

    public bool IsValid() =>
        RateShrinkageMinutes > 0m
        && LeagueAverageMinimumGames >= 0
        && FullGamesThreshold > 0
        && MinutesPrior is >= 0m and <= 42m
        && GamesPriorWeight is >= 0m and <= 1m
        && Durability is >= 0m and <= 1m
        && SeasonGames > 0
        && !string.IsNullOrWhiteSpace(ModelVersion);
}

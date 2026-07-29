using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Leagues;

public sealed record ScoringRule(StatKey Stat, decimal PointsPerUnit);

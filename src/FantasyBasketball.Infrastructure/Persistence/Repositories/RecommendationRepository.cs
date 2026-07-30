using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class RecommendationRepository(FantasyDbContext database)
    : IRecommendationRepository
{
    public async Task AddRangeAsync(
        IReadOnlyList<Recommendation> recommendations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recommendations);
        foreach (var recommendation in recommendations)
        {
            var row = RecommendationRow.Create(
                recommendation.Id,
                recommendation.Action,
                recommendation.SubjectPlayerId?.Value,
                Round(recommendation.Score),
                recommendation.Confidence.ToString());
            row.Evidence.AddRange(recommendation.Evidence.Select((evidence, ordinal) =>
                RecommendationEvidenceRow.Create(
                    recommendation.Id,
                    evidence.Kind.ToString(),
                    evidence.Polarity.ToString(),
                    evidence.Statement,
                    evidence.Magnitude is { } magnitude
                        ? Round(magnitude)
                        : null,
                    ordinal)));
            database.Recommendations.Add(row);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Recommendation?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await database.Recommendations
            .AsNoTracking()
            .Include(value => value.Evidence)
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return row is null
            ? null
            : new Recommendation(
                row.Id,
                row.Action,
                row.SubjectPlayerId is { } playerId
                    ? new PlayerId(playerId)
                    : null,
                row.Score,
                Enum.Parse<Confidence>(row.Confidence),
                row.Evidence
                    .OrderBy(value => value.Ordinal)
                    .Select(value => new RecommendationEvidence(
                        Enum.Parse<EvidenceKind>(value.Kind),
                        Enum.Parse<EvidencePolarity>(value.Polarity),
                        value.Statement,
                        value.Magnitude))
                    .ToArray());
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

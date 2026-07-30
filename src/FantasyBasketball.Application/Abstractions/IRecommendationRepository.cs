using FantasyBasketball.Domain.Recommendations;

namespace FantasyBasketball.Application.Abstractions;

public interface IRecommendationRepository
{
    Task AddRangeAsync(
        IReadOnlyList<Recommendation> recommendations,
        CancellationToken cancellationToken);

    Task<Recommendation?> GetAsync(
        Guid id,
        CancellationToken cancellationToken);
}

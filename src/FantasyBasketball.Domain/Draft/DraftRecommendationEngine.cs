using FantasyBasketball.Domain.Recommendations;

namespace FantasyBasketball.Domain.Draft;

public sealed class DraftRecommendationEngine(
    ConfidenceCalculator confidenceCalculator)
{
    public IReadOnlyList<Recommendation> Recommend(
        DraftBoardResult board,
        ConfidenceFactors confidenceFactors)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(confidenceFactors);
        var confidence = confidenceCalculator.Calculate(confidenceFactors);
        return board.Rankings
            .Select(value => new Recommendation(
                Guid.NewGuid(),
                "Draft now",
                value.PlayerId,
                value.Total,
                confidence,
                value.Evidence))
            .ToArray();
    }
}

using System.Globalization;
using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Trends;

public sealed record PlayerHeatLabel(
    Guid PlayerId,
    string Label,
    string? Cause,
    decimal Probability,
    decimal ShiftMean,
    decimal ShiftLow,
    decimal ShiftHigh,
    decimal? OpportunityShare,
    int RecentGames,
    int BaselineGames,
    DateOnly ThroughDate);

public sealed record HeatLabelPage(
    int SeasonEndYear,
    DateOnly ThroughDate,
    string ModelVersion,
    int EligiblePlayers,
    int QualifiedPlayers,
    decimal? NullLabelRate,
    int? ExpectedChanceLabels,
    string Disclaimer,
    IReadOnlyList<PlayerHeatLabel> Labels);

/// <summary>
/// HOT/COLD labels for one league's scoring, from the active "heat-prior" model. Descriptive
/// only (player_heat_contract): every page carries the disclaimer and the chance rate.
/// </summary>
public sealed class HeatLabelService(
    ILeagueRepository leagues,
    IBoxScoreRepository games,
    IModelVersionRepository models,
    PointsScoringEngine scoring)
{
    // 80% interval for the shift shown beside each label.
    private const decimal Z80 = 1.2816m;

    public async Task<HeatLabelPage> QueryAsync(
        Guid leagueId,
        int seasonEndYear,
        string source,
        DateOnly throughDate,
        CancellationToken token)
    {
        var league = await leagues.GetAsync(leagueId, token) ?? throw new ResourceNotFoundException("League was not found.");
        if (league.Type != LeagueType.Points)
        {
            throw new ResourceConflictException("Heat labels require a points league.");
        }

        var model = await models.GetActiveAsync(HeatPriorParameters.ModelName, token)
            ?? throw new ResourceConflictException("Heat labels need a fitted heat-prior model; run tools/modeling/heat_prior.py and import it.");
        var prior = HeatPriorParameters.Parse(model.ParametersJson);
        var classifier = new HeatClassifier(prior);
        var seasons = (await games.ListAsync(seasonEndYear, source, NbaGamePhase.RegularSeason, throughDate, token))
            .Where(sample => sample is { IsFinal: true, DidPlay: true, Statistics: not null })
            .GroupBy(sample => sample.PlayerId.Value)
            .ToDictionary(group => group.Key, group => Appearances(group, league));
        var eligible = seasons.Count(pair => pair.Value.Count >= prior.RecentGames + prior.MinBaselineGames);
        var assessments = seasons
            .Select(pair => (PlayerId: pair.Key, Assessment: classifier.Classify(pair.Value)))
            .Where(pair => pair.Assessment is not null)
            .ToArray();
        var nullRate = NullLabelRate(model.MetricsJson);
        var labels = assessments
            .Where(pair => pair.Assessment!.Label != HeatLabel.None)
            .Select(pair => Label(pair.PlayerId, pair.Assessment!, prior.RecentGames))
            .OrderByDescending(label => label.Probability)
            .ThenBy(label => label.PlayerId)
            .ToArray();
        return new HeatLabelPage(
            seasonEndYear,
            throughDate,
            model.Version,
            eligible,
            assessments.Length,
            nullRate,
            nullRate is { } rate ? (int)Math.Round(rate * eligible, MidpointRounding.AwayFromZero) : null,
            Disclaimer(prior, nullRate),
            labels);
    }

    public static string Disclaimer(HeatPriorParameters prior, decimal? nullRate)
    {
        ArgumentNullException.ThrowIfNull(prior);
        var chance = nullRate is { } rate
            ? $" About {(rate * 100m).ToString("0", CultureInfo.InvariantCulture)}% of players get a label like this by chance alone."
            : string.Empty;
        return $"Describes the last {prior.RecentGames} games against the {prior.MinBaselineGames}–{prior.MaxBaselineGames} before them. Not a forecast.{chance}";
    }

    private List<HeatAppearance> Appearances(IEnumerable<Domain.Trends.PlayerGameSample> samples, FantasyLeague league) =>
        samples
            .OrderBy(sample => sample.PlayedOn)
            .ThenBy(sample => sample.GameId)
            .Select(sample => new HeatAppearance(
                sample.PlayedOn,
                scoring.Score(sample.Statistics!, league),
                sample.Statistics![StatKey.MIN],
                sample.Statistics[StatKey.FGA],
                sample.Statistics[StatKey.FTA],
                sample.Statistics[StatKey.TOV]))
            .ToList();

    private static PlayerHeatLabel Label(Guid playerId, HeatAssessment assessment, int recentGames) =>
        new(
            playerId,
            assessment.Label.ToString().ToUpperInvariant(),
            assessment.Cause?.ToString(),
            assessment.Probability,
            assessment.Posterior.ShiftMean,
            assessment.Posterior.ShiftMean - (Z80 * assessment.Posterior.ShiftSd),
            assessment.Posterior.ShiftMean + (Z80 * assessment.Posterior.ShiftSd),
            assessment.Decomposition?.OpportunityShare,
            recentGames,
            assessment.BaselineGames,
            assessment.ThroughDate);

    private static decimal? NullLabelRate(string metricsJson)
    {
        using var metrics = JsonDocument.Parse(metricsJson);
        return metrics.RootElement.TryGetProperty("falseLabels", out var falseLabels)
            && falseLabels.TryGetProperty("nullLabelRate", out var rate)
            ? rate.GetDecimal()
            : null;
    }
}

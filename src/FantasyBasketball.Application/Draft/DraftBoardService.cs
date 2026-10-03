using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Health;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Schedule;

namespace FantasyBasketball.Application.Draft;

/// <summary>A simulated board, or why none could be run.</summary>
public sealed record SimulatedDraftBoard(SimulatedBoard? Board, string? Unavailable);

public sealed class DraftBoardService(
    IDraftRepository drafts,
    ILeagueRepository leagues,
    IDraftCandidateRepository candidates,
    DraftBoard board,
    DraftRecommendationEngine recommendationEngine,
    DataSourceHealthService health,
    IRecommendationRepository recommendationRepository,
    IGameRepository games,
    IModelVersionRepository models,
    DraftSimulator simulator,
    TimeProvider clock)
{
    /// <summary>A regular season has 1,230 games; fewer means the schedule is not fully imported.</summary>
    private const int FullScheduleGames = 1000;
    public async Task<DraftBoardResult> GetBoardAsync(
        Guid draftSessionId,
        CancellationToken cancellationToken)
    {
        var state = await drafts.GetSessionAsync(
            draftSessionId,
            cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"Draft session '{draftSessionId}' was not found.");
        var league = await leagues.GetAsync(state.LeagueId, cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"League '{state.LeagueId}' was not found.");
        var pool = await candidates.ListAsync(
            state.LeagueId,
            cancellationToken);
        var userRoster = state.Session.Picks
            .Where(pick => state.Session.IsUserPick(pick.PickNumber))
            .Select(pick => pick.PlayerId)
            .ToArray();
        return board.Rank(state.Session, league, pool, userRoster);
    }

    /// <summary>
    /// Monte Carlo lookahead for the user's pick (draft_simulation): needs a points league,
    /// published distributions and a full NBA schedule, else says which is missing.
    /// </summary>
    public async Task<SimulatedDraftBoard> SimulateAsync(Guid draftSessionId, RiskMode mode, CancellationToken cancellationToken)
    {
        var state = await drafts.GetSessionAsync(draftSessionId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Draft session '{draftSessionId}' was not found.");
        var league = await leagues.GetAsync(state.LeagueId, cancellationToken)
            ?? throw new ResourceNotFoundException($"League '{state.LeagueId}' was not found.");
        var session = state.Session;
        if (league.Type != FantasyBasketball.Domain.Leagues.LeagueType.Points)
        {
            return new SimulatedDraftBoard(null, "Simulation needs a points league.");
        }

        if (session.CurrentPick > session.TeamCount * session.RoundCount)
        {
            return new SimulatedDraftBoard(null, "The draft is complete.");
        }

        var pool = await candidates.ListAsync(state.LeagueId, cancellationToken);
        if (!pool.Any(candidate => candidate.Distribution is not null))
        {
            return new SimulatedDraftBoard(null, "Recalculate projections with the fitted models active to get distributions to simulate.");
        }

        if (await ScheduleAsync(cancellationToken) is not { } schedule)
        {
            return new SimulatedDraftBoard(null, "Import a full NBA schedule to simulate weekly lineups.");
        }

        var choice = await models.GetActiveAsync(OpponentChoiceParameters.ModelName, cancellationToken) is { } fitted
            ? new OpponentChoiceModel(OpponentChoiceParameters.Parse(fitted.ParametersJson))
            : null;
        var lineup = new LineupOptimizer(league.RosterSlots, schedule, league.Cadence);
        // Seeded by draft and pick, so a refresh at the same pick shows the same numbers.
        var seed = BitConverter.ToInt32(draftSessionId.ToByteArray()) ^ (session.CurrentPick * 7919);
        return new SimulatedDraftBoard(simulator.Simulate(session, lineup, pool, seed, mode, choice), null);
    }

    public async Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(
        Guid draftSessionId,
        CancellationToken cancellationToken)
    {
        var ranked = Lead(
            await GetBoardAsync(draftSessionId, cancellationToken),
            (await SimulateAsync(draftSessionId, RiskMode.Mean, cancellationToken)).Board);
        var sources = await health.GetAsync(cancellationToken);
        var automated = sources.Where(value =>
            value.Source == DataSourceName.BallDontLie
            || value.Source == DataSourceName.BasketballReference
            || value.Source == DataSourceName.FantasyPros).ToArray();
        var freshness = automated.Length == 0
            ? 0.5m
            : automated.Average(value => value.IsStale ? 0.25m : 1m);
        var recommendations = recommendationEngine.Recommend(
            ranked,
            new ConfidenceFactors(
                SampleSize: 0.5m,
                RoleStability: 0.5m,
                DataFreshness: freshness,
                SourceQuality: 0.8m,
                ContextCertainty: 0.5m));
        IReadOnlyList<Recommendation> final = recommendations;
        if (automated.Any(value => value.IsDegraded))
        {
            var quality = new RecommendationEvidence(
                EvidenceKind.DataQuality,
                EvidencePolarity.Risk,
                "One or more contributing data sources are stale or failed",
                automated.Count(value => value.IsDegraded));
            final = recommendations.Select(recommendation => new Recommendation(
                recommendation.Id,
                recommendation.Action,
                recommendation.SubjectPlayerId,
                recommendation.Score,
                recommendation.Confidence,
                recommendation.Evidence.Append(quality).ToArray()))
                .ToArray();
        }

        await recommendationRepository.AddRangeAsync(final, cancellationToken);
        return final;
    }

    /// <summary>
    /// The simulated candidates lead, in simulated order, each with its edge and survival as
    /// evidence; the rest keep the heuristic order behind them.
    /// </summary>
    private static DraftBoardResult Lead(DraftBoardResult heuristic, SimulatedBoard? simulated)
    {
        if (simulated is not { Candidates.Count: > 0 })
        {
            return heuristic;
        }

        var byPlayer = heuristic.Rankings.ToDictionary(value => value.PlayerId);
        var lead = simulated.Candidates.Where(candidate => byPlayer.ContainsKey(candidate.PlayerId)).Select((candidate, index) =>
        {
            var value = byPlayer[candidate.PlayerId];
            var evidence = value.Evidence.Append(index == 0
                ? new RecommendationEvidence(EvidenceKind.Opportunity, EvidencePolarity.Supporting,
                    $"Best simulated pick: final roster {candidate.Mean:0} ± {candidate.Sd:0} season points over {simulated.Rollouts} rollouts", candidate.Mean)
                : new RecommendationEvidence(EvidenceKind.Opportunity, EvidencePolarity.Risk,
                    $"Simulated final roster {candidate.Edge:0} ± {candidate.EdgeSe:0} season points against the top pick", candidate.Edge));
            if (candidate.SurvivalToNextPick is { } survival)
            {
                evidence = evidence.Append(new RecommendationEvidence(EvidenceKind.Market,
                    survival < 0.5m ? EvidencePolarity.Supporting : EvidencePolarity.Neutral,
                    $"About {(int)Math.Round(survival * 100m)}% chance he is still there at pick {simulated.NextUserPick}", survival));
            }

            return new DraftValue(value.PlayerId, value.Total, value.ProjectedSeasonValue, value.ValueAboveReplacement,
                value.PositionalScarcity, value.RosterFit, value.ContextAdjustment, value.InjuryRisk, value.RoleRisk, evidence.ToArray());
        }).ToArray();
        var leading = lead.Select(value => value.PlayerId).ToHashSet();
        return new DraftBoardResult([.. lead, .. heuristic.Rankings.Where(value => !leading.Contains(value.PlayerId))], heuristic.Banner);
    }

    /// <summary>The season ahead when its schedule is imported, else the latest complete regular season.</summary>
    private async Task<SeasonSchedule?> ScheduleAsync(CancellationToken cancellationToken)
    {
        // ponytail: reads ~1,230 games per request; cache per season if board latency needs it.
        var now = clock.GetUtcNow();
        var ahead = await games.ListScheduledAsync(DataSourceName.BallDontLie, now.AddDays(-14), now.AddDays(200), cancellationToken);
        if (ahead.Count >= FullScheduleGames)
        {
            return new SeasonSchedule(ahead);
        }

        for (var year = now.Year; year > now.Year - 4; year--)
        {
            var end = new DateTimeOffset(year, 4, 20, 0, 0, 0, TimeSpan.Zero); // regular season only
            if (end > now)
            {
                continue;
            }

            var season = await games.ListScheduledAsync(DataSourceName.BallDontLie, new DateTimeOffset(year - 1, 10, 1, 0, 0, 0, TimeSpan.Zero), end, cancellationToken);
            if (season.Count >= FullScheduleGames)
            {
                return new SeasonSchedule(season);
            }
        }

        return null;
    }
}

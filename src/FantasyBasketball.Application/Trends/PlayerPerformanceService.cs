using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Trends;

public sealed record PerformanceScoringRule(string Stat, decimal PointsPerUnit);

public sealed record PlayerPerformancePage(
    int SeasonEndYear, string Source, DateOnly ThroughDate, string View,
    string ModelVersion, PlayerHeatOptions Policy, IReadOnlyList<PerformanceScoringRule> ScoringRules, int ObservedPlayers,
    int BestQualifiedPlayers, int ComparisonQualifiedPlayers,
    DateOnly? LatestAppearance, DateTimeOffset? LatestFetchedAt,
    IReadOnlyList<PlayerHeatResult> Players, int Total);

public sealed class PlayerPerformanceService(
    ILeagueRepository leagues, IBoxScoreRepository games,
    PlayerHeatCalculator calculator, PlayerHeatOptions policy)
{
    public async Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(Guid leagueId, CancellationToken token)
    {
        await RequirePointsLeagueAsync(leagueId, token);
        return await games.ListPoolsAsync(token);
    }

    public async Task<PlayerPerformancePage> QueryAsync(Guid leagueId, int seasonEndYear,
        string source, DateOnly throughDate, string view, int page, int limit, CancellationToken token)
    {
        var league = await RequirePointsLeagueAsync(leagueId, token);
        Paging.Validate(page, limit);
        if (view is not ("best" or "hot" or "all")) throw new ArgumentException("Select best, hot or all.", nameof(view));
        var samples = await games.ListAsync(seasonEndYear, source, NbaGamePhase.RegularSeason, throughDate, token);
        var results = samples.GroupBy(sample => sample.PlayerId).Select(group =>
        {
            token.ThrowIfCancellationRequested();
            return calculator.Calculate(group.Key, league, seasonEndYear, throughDate, group.ToArray());
        }).ToArray();
        var rankings = calculator.Rank(league, seasonEndYear, throughDate, samples);
        IEnumerable<PlayerHeatResult> selected = view switch
        {
            "best" => rankings.BestPerforming,
            "hot" => rankings.Hottest,
            _ => results.OrderBy(result => result.PlayerId.Value),
        };
        var ranked = selected.ToArray();
        return new PlayerPerformancePage(seasonEndYear, source, throughDate, view,
            PlayerHeatOptions.ModelVersion, policy,
            league.ScoringRules.Select(rule => new PerformanceScoringRule(rule.Stat.ToString(), rule.PointsPerUnit)).ToArray(), results.Length,
            results.Count(result => result.CurrentAverage.HasValue), results.Count(result => result.HasComparison),
            results.Select(result => result.LatestAppearance).Max(),
            samples.Select(sample => (DateTimeOffset?)sample.Provenance.FetchedAt).Max(),
            ranked.Skip((int)Math.Min((long)(page - 1) * limit, int.MaxValue)).Take(limit).ToArray(), ranked.Length);
    }

    private async Task<FantasyLeague> RequirePointsLeagueAsync(Guid id, CancellationToken token)
    {
        var league = await leagues.GetAsync(id, token) ?? throw new ResourceNotFoundException("League was not found.");
        if (league.Type != LeagueType.Points) throw new ResourceConflictException("Recorded performance requires a points league.");
        return league;
    }
}

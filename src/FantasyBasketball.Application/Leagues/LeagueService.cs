using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Leagues;

namespace FantasyBasketball.Application.Leagues;

public sealed class LeagueService(ILeagueRepository leagues)
{
    public async Task<FantasyLeague> CreateAsync(
        FantasyLeague league,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(league);
        await leagues.AddAsync(league, cancellationToken);
        return league;
    }

    public async Task<FantasyLeague> GetAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await leagues.GetAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException($"League '{id}' was not found.");

    public async Task<IReadOnlyList<FantasyLeague>> ListAsync(
        CancellationToken cancellationToken) =>
        await leagues.ListAsync(cancellationToken);

    public async Task<FantasyLeague> ReplaceScoringAsync(
        Guid id,
        IReadOnlyList<ScoringRule> scoringRules,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scoringRules);
        var existing = await GetAsync(id, cancellationToken);
        if (existing.Type != LeagueType.Points)
        {
            throw new ResourceConflictException(
                "Scoring rules can only be replaced for a points league.");
        }

        var replacement = new FantasyLeague(
            existing.Id,
            existing.Name,
            existing.Type,
            existing.TeamCount,
            scoringRules,
            existing.Categories,
            existing.RosterSlots,
            existing.Cadence);
        await leagues.SaveScoringAsync(replacement, cancellationToken);
        return replacement;
    }
}

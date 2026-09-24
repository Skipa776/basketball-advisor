using FantasyBasketball.Domain.Players;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Landing;
using FantasyBasketball.Domain.Leagues;

namespace FantasyBasketball.Application.Leagues;

/// <summary>
/// The league's waiver view: risers under the league's own scoring, leaving out every
/// rostered player. Without imported rosters it leaves out the featured stars instead
/// and says so, rather than presenting rostered players as available.
/// </summary>
public sealed class LeagueWaiverService(ILeagueRepository leagues, ILeagueTeamRepository teams, LandingService landing,
    IAvailabilityRepository availability)
{
    public async Task<LandingRisers> RisersAsync(Guid leagueId, DateOnly? throughDate, int limit, CancellationToken token)
    {
        var league = await leagues.GetAsync(leagueId, token) ?? throw new ResourceNotFoundException("League was not found.");
        if (league.Type != LeagueType.Points)
        {
            throw new ResourceConflictException("The waiver list uses points scoring; category leagues are not supported yet.");
        }

        var rostered = (await teams.ListAsync(leagueId, token)).SelectMany(team => team.Players).ToHashSet();
        var risers = rostered.Count > 0
            ? await landing.RankRisersAsync(throughDate, limit, league, rostered, "rostered", token)
            : await landing.RisersExcludingFeaturedAsync(throughDate, limit, league, token);
        var injuries = await availability.ListAsync(token);
        // A "Must add" who is out tonight should say so, when the report is current for this list.
        return risers.ThroughDate is { } through && CurrentAvailability.Applies(injuries, through.AddDays(1))
            ? risers with { Players = risers.Players.Select(player => player with { Injury = CurrentAvailability.Label(injuries, new PlayerId(player.PlayerId)) }).ToArray() }
            : risers;
    }
}

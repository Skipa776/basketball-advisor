using FantasyBasketball.Application.Leagues;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Api.Middleware;

namespace FantasyBasketball.Api.Endpoints;

public sealed record ScoringRuleRequest(string Stat, decimal PointsPerUnit);

public sealed record CreateLeagueRequest(
    string Name,
    string Type,
    int TeamCount,
    IReadOnlyList<ScoringRuleRequest>? ScoringRules,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<string>? RosterSlots,
    string Cadence);

public sealed record ReplaceScoringRequest(
    IReadOnlyList<ScoringRuleRequest>? ScoringRules);

public static class LeagueEndpoints
{
    public static IEndpointRouteBuilder MapLeagueEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/leagues");
        group.AddEndpointFilter<CookieAntiforgeryFilter>();
        group.RequireAuthorization();
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapPut("/{id:guid}/scoring", ReplaceScoringAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        return endpoints;
    }

    public static async Task<IResult> CreateAsync(
        CreateLeagueRequest request,
        LeagueService service,
        CancellationToken cancellationToken)
    {
        var league = BuildLeague(Guid.NewGuid(), request);
        return ApiResults.Success(
            await service.CreateAsync(league, cancellationToken),
            StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetAsync(
        Guid id,
        LeagueService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken) =>
        ApiResults.Success(await RequireAndGetAsync(
            id,
            service,
            authorization,
            cancellationToken));

    public static async Task<IResult> ReplaceScoringAsync(
        Guid id,
        ReplaceScoringRequest request,
        LeagueService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        var rules = ParseScoringRules(request.ScoringRules);
        return ApiResults.Success(
            await service.ReplaceScoringAsync(id, rules, cancellationToken));
    }

    private static async Task<FantasyLeague> RequireAndGetAsync(
        Guid id,
        LeagueService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        return await service.GetAsync(id, cancellationToken);
    }

    private static FantasyLeague BuildLeague(
        Guid id,
        CreateLeagueRequest request)
    {
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            fields["name"] = ["Name is required."];
        }

        if (!Enum.TryParse<LeagueType>(
                request.Type,
                ignoreCase: true,
                out var type))
        {
            fields["type"] = ["Type must be Points or Categories."];
        }

        if (request.TeamCount <= 0)
        {
            fields["teamCount"] = ["Team count must be greater than zero."];
        }

        if (!Enum.TryParse<LineupCadence>(
                request.Cadence,
                ignoreCase: true,
                out var cadence))
        {
            fields["cadence"] = ["Cadence must be Daily or Weekly."];
        }

        var rosterSlots = ParseRosterSlots(request.RosterSlots, fields);
        var scoringRules = ParseScoringRules(request.ScoringRules, fields);
        var categories = ParseStats(
            request.Categories,
            "categories",
            fields);
        if (type == LeagueType.Points && scoringRules.Count == 0)
        {
            fields["scoringRules"] = [
                "A points league requires at least one scoring rule.",
            ];
        }

        if (type == LeagueType.Categories && categories.Count == 0)
        {
            fields["categories"] = [
                "A category league requires at least one category.",
            ];
        }

        ThrowIfInvalid(fields);
        return new FantasyLeague(
            id,
            request.Name,
            type,
            request.TeamCount,
            scoringRules,
            categories,
            rosterSlots,
            cadence);
    }

    private static IReadOnlyList<ScoringRule> ParseScoringRules(
        IReadOnlyList<ScoringRuleRequest>? requests)
    {
        var fields = new Dictionary<string, string[]>();
        var rules = ParseScoringRules(requests, fields);
        ThrowIfInvalid(fields);
        return rules;
    }

    private static IReadOnlyList<ScoringRule> ParseScoringRules(
        IReadOnlyList<ScoringRuleRequest>? requests,
        IDictionary<string, string[]> fields)
    {
        if (requests is null)
        {
            return [];
        }

        var rules = new List<ScoringRule>();
        foreach (var request in requests)
        {
            if (!Enum.TryParse<StatKey>(
                    request.Stat,
                    ignoreCase: true,
                    out var stat))
            {
                fields["scoringRules"] = [
                    $"Unknown scoring stat '{request.Stat}'.",
                ];
                continue;
            }

            rules.Add(new ScoringRule(stat, request.PointsPerUnit));
        }

        if (rules.GroupBy(rule => rule.Stat).Any(group => group.Count() > 1))
        {
            fields["scoringRules"] = ["Scoring stats cannot be duplicated."];
        }

        return rules;
    }

    private static IReadOnlyList<StatKey> ParseStats(
        IReadOnlyList<string>? values,
        string field,
        IDictionary<string, string[]> fields)
    {
        if (values is null)
        {
            return [];
        }

        var results = new List<StatKey>();
        foreach (var value in values)
        {
            if (!Enum.TryParse<StatKey>(value, true, out var stat))
            {
                fields[field] = [$"Unknown stat '{value}'."];
            }
            else
            {
                results.Add(stat);
            }
        }

        if (results.Distinct().Count() != results.Count)
        {
            fields[field] = ["Stats cannot be duplicated."];
        }

        return results;
    }

    private static IReadOnlyList<RosterSlot> ParseRosterSlots(
        IReadOnlyList<string>? values,
        IDictionary<string, string[]> fields)
    {
        if (values is null || values.Count == 0)
        {
            fields["rosterSlots"] = ["At least one roster slot is required."];
            return [];
        }

        var results = new List<RosterSlot>();
        foreach (var value in values)
        {
            if (!Enum.TryParse<RosterSlotKind>(value, true, out var kind))
            {
                fields["rosterSlots"] = [$"Unknown roster slot '{value}'."];
            }
            else
            {
                results.Add(new RosterSlot(kind));
            }
        }

        return results;
    }

    private static void ThrowIfInvalid(
        IReadOnlyDictionary<string, string[]> fields)
    {
        if (fields.Count > 0)
        {
            throw new RequestValidationException(fields);
        }
    }
}

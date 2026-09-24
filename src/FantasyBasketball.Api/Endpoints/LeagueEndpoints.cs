using FantasyBasketball.Application.Leagues;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Api.Middleware;

namespace FantasyBasketball.Api.Endpoints;

public sealed record RosterCsvRequest(string? Csv);

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

public sealed record UpdateLeagueSettingsRequest(
    string Name,
    int TeamCount,
    string Cadence,
    IReadOnlyList<string>? RosterSlots);

public sealed record RecalculateProjectionsRequest(int SeasonEndYear, string Source);

public static class LeagueEndpoints
{
    public static IEndpointRouteBuilder MapLeagueEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/leagues");
        group.AddEndpointFilter<CookieAntiforgeryFilter>();
        group.RequireAuthorization();
        group.MapGet("/", ListAsync);
        group.MapGet("/setup", () => ApiResults.Success(LeagueSetupCatalog.Create()));
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapPut("/{id:guid}/scoring", ReplaceScoringAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapPut("/{id:guid}/settings", UpdateSettingsAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapGet("/{id:guid}/projection-pools", ProjectionPoolsAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapPost("/{id:guid}/projections", RecalculateProjectionsAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapGet("/{id:guid}/projected-players", ProjectedPlayersAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapGet("/{id:guid}/performance-pools", PerformanceEndpoints.PoolsAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapGet("/{id:guid}/performance", PerformanceEndpoints.QueryAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapGet("/{id:guid}/teams", ListTeamsAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        group.MapPost("/{id:guid}/teams/csv", ImportTeamsCsvAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        return endpoints;
    }

    public static async Task<IResult> ProjectionPoolsAsync(
        Guid id, LeagueProjectionService service,
        OwnedResourceAuthorizationService authorization, CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        return ApiResults.Success(await service.ListPoolsAsync(cancellationToken));
    }

    public static async Task<IResult> ListTeamsAsync(
        Guid id, LeagueRosterService service, OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        return ApiResults.Success(await service.ListAsync(id, cancellationToken));
    }

    public static async Task<IResult> ImportTeamsCsvAsync(
        Guid id, RosterCsvRequest request, LeagueRosterService service,
        OwnedResourceAuthorizationService authorization, CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        try
        {
            return ApiResults.Success(await service.ImportCsvAsync(id, request.Csv ?? string.Empty, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["csv"] = [exception.Message] });
        }
    }

    public static async Task<IResult> ProjectedPlayersAsync(
        Guid id, int? page, int? limit, ProjectedPlayerService service,
        OwnedResourceAuthorizationService authorization, CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        var currentPage = page ?? 1;
        var pageSize = limit ?? Paging.DefaultLimit;
        Paging.Validate(currentPage, pageSize);
        var result = await service.ListAsync(id, currentPage, pageSize, cancellationToken);
        return ApiResults.Success(result.Items, meta: new ApiMeta(result.Total, result.Page, result.Limit));
    }

    public static async Task<IResult> RecalculateProjectionsAsync(
        Guid id, RecalculateProjectionsRequest request, LeagueProjectionService service,
        OwnedResourceAuthorizationService authorization, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        if (request.SeasonEndYear < 1947 || request.SeasonEndYear > clock.GetUtcNow().Year + 1
            || string.IsNullOrWhiteSpace(request.Source) || !DataSourceName.IsKnown(request.Source))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["pool"] = ["Select an imported season and known source."],
            });
        }

        return ApiResults.Success(await service.RecalculateAsync(
            id, request.SeasonEndYear, request.Source, cancellationToken));
    }

    public static async Task<IResult> ListAsync(
        int? page,
        int? limit,
        LeagueService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var pageSize = limit ?? Paging.DefaultLimit;
        Paging.Validate(currentPage, pageSize);
        var leagues = await service.ListAsync(cancellationToken);
        var selected = leagues.Skip((int)Math.Min((long)(currentPage - 1) * pageSize, int.MaxValue))
            .Take(pageSize).ToArray();
        foreach (var league in selected)
        {
            await authorization.RequireLeagueAsync(league.Id, cancellationToken);
        }

        return ApiResults.Success(selected, meta: new ApiMeta(leagues.Count, currentPage, pageSize));
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

    public static async Task<IResult> UpdateSettingsAsync(
        Guid id,
        UpdateLeagueSettingsRequest request,
        LeagueService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        var fields = new Dictionary<string, string[]>();
        ValidateName(request.Name, fields);

        if (request.TeamCount <= 0)
        {
            fields["teamCount"] = ["Team count must be greater than zero."];
        }

        if (!Enum.TryParse<LineupCadence>(request.Cadence, true, out var cadence)
            || !Enum.IsDefined(cadence))
        {
            fields["cadence"] = ["Cadence must be Daily or Weekly."];
        }

        var slots = ParseRosterSlots(request.RosterSlots, fields);
        ThrowIfInvalid(fields);
        return ApiResults.Success(await service.UpdateSettingsAsync(
            id,
            request.Name,
            request.TeamCount,
            cadence,
            slots,
            cancellationToken));
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

    // Matches the maxLength on every league name input.
    private const int MaxNameLength = 100;

    private static void ValidateName(string? name, Dictionary<string, string[]> fields)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            fields["name"] = ["Name is required."];
        }
        else if (name.Length > MaxNameLength)
        {
            fields["name"] = [$"Name must be {MaxNameLength} characters or fewer."];
        }
    }

    private static FantasyLeague BuildLeague(
        Guid id,
        CreateLeagueRequest request)
    {
        var fields = new Dictionary<string, string[]>();
        ValidateName(request.Name, fields);

        if (!Enum.TryParse<LeagueType>(
                request.Type,
                ignoreCase: true,
                out var type) || !Enum.IsDefined(type))
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
                out var cadence) || !Enum.IsDefined(cadence))
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
                    out var stat) || !Enum.IsDefined(stat))
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
            if (!Enum.TryParse<StatKey>(value, true, out var stat) || !Enum.IsDefined(stat))
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
            if (!Enum.TryParse<RosterSlotKind>(value, true, out var kind) || !Enum.IsDefined(kind))
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

using System.Globalization;
using FantasyBasketball.Application.Landing;

namespace FantasyBasketball.Api.Endpoints;

/// <summary>Anonymous, read-only NBA reference data for the landing page. No user data.</summary>
public static class PublicEndpoints
{
    public const string RateLimitPolicy = "public";

    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/public").AllowAnonymous().RequireRateLimiting(RateLimitPolicy);
        group.MapGet("/daily", DailyAsync);
        group.MapGet("/risers", RisersAsync);
        return endpoints;
    }

    public static async Task<IResult> DailyAsync(
        string? date, LandingService service, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        return ApiResults.Success(await service.DailyAsync(ParseDate(date, nameof(date)), cancellationToken));
    }

    public static async Task<IResult> RisersAsync(
        string? through, int? limit, LandingService service, HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var size = limit ?? 25;
        if (size is < 1 or > 50)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["limit"] = ["Limit must be between 1 and 50."],
            });
        }

        return ApiResults.Success(await service.RisersAsync(ParseDate(through, nameof(through)), size, cancellationToken));
    }

    private static DateOnly? ParseDate(string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [field] = ["Use a date in YYYY-MM-DD format."],
            });
    }
}

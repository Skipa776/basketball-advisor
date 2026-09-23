using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Api.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace FantasyBasketball.Api.Endpoints;

public sealed record RegisterAccountRequest(
    string Email,
    string Password,
    string DisplayName);

public sealed record LoginAccountRequest(
    string Email,
    string Password,
    bool RememberMe);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account");
        group.AddEndpointFilter<CookieAntiforgeryFilter>();
        group.MapGet("/session", GetSessionAsync).AllowAnonymous();
        group.MapGet("/antiforgery", GetAntiforgery)
            .AllowAnonymous();
        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting("account");
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("account");
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization();
        group.MapGet("/export", ExportAsync)
            .RequireAuthorization();
        group.MapPost("/import", ImportAsync)
            .RequireAuthorization();
        group.MapDelete("/", DeleteAsync)
            .RequireAuthorization();
        return endpoints;
    }

    public static async Task<IResult> GetSessionAsync(
        HttpContext context,
        UserManager<FantasyUser> users,
        RegistrationService registrations,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var user = context.User.Identity?.IsAuthenticated is true
            ? await users.GetUserAsync(context.User)
            : null;
        return ApiResults.Success(new
        {
            Authenticated = user is not null,
            User = user is null ? null : new { user.Id, user.DisplayName, user.IsInstanceOwner },
            RegistrationOpen = await registrations.IsOpenAsync(cancellationToken),
        });
    }

    public static async Task<IResult> ExportAsync(
        AccountDataService accounts,
        CancellationToken cancellationToken) =>
        ApiResults.Success(await accounts.ExportAsync(cancellationToken));

    public static async Task<IResult> ImportAsync(
        OwnedDataArchive archive,
        AccountDataService accounts,
        CancellationToken cancellationToken)
    {
        await accounts.ImportAsync(archive, cancellationToken);
        return ApiResults.Success(new { Imported = true });
    }

    public static async Task<IResult> DeleteAsync(
        AccountDataService accounts,
        SignInManager<FantasyUser> signIn,
        CancellationToken cancellationToken)
    {
        await accounts.DeleteAsync(cancellationToken);
        await signIn.SignOutAsync();
        return ApiResults.Success(new { Deleted = true });
    }

    public static IResult GetAntiforgery(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return ApiResults.Success(new { Token = tokens.RequestToken });
    }

    public static async Task<IResult> RegisterAsync(
        RegisterAccountRequest request,
        RegistrationService registrations,
        SignInManager<FantasyUser> signIn,
        CancellationToken cancellationToken)
    {
        var result = await registrations.RegisterAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            cancellationToken);
        if (result.RegistrationClosed)
        {
            return ApiResults.Failure(
                StatusCodes.Status404NotFound,
                "not_found",
                "The requested resource was not found.");
        }

        if (!result.Succeeded)
        {
            throw new RequestValidationException(
                new Dictionary<string, string[]>
                {
                    ["account"] = result.Errors.ToArray(),
                });
        }

        await signIn.SignInAsync(
            result.User!,
            isPersistent: false);
        return ApiResults.Success(
            new
            {
                result.User!.Id,
                result.User.DisplayName,
                result.User.IsInstanceOwner,
            },
            StatusCodes.Status201Created);
    }

    public static async Task<IResult> LoginAsync(
        LoginAccountRequest request,
        UserManager<FantasyUser> users,
        SignInManager<FantasyUser> signIn)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return ApiResults.Failure(
                StatusCodes.Status401Unauthorized,
                "invalid_credentials",
                "Email or password is incorrect.");
        }

        var result = await signIn.PasswordSignInAsync(
            user,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return ApiResults.Failure(
                StatusCodes.Status401Unauthorized,
                "locked_out",
                "The account is temporarily locked.");
        }

        return result.Succeeded
            ? ApiResults.Success(new
            {
                user.Id,
                user.DisplayName,
                user.IsInstanceOwner,
            })
            : ApiResults.Failure(
                StatusCodes.Status401Unauthorized,
                "invalid_credentials",
                "Email or password is incorrect.");
    }

    public static async Task<IResult> LogoutAsync(
        SignInManager<FantasyUser> signIn)
    {
        await signIn.SignOutAsync();
        return ApiResults.Success(new { SignedOut = true });
    }
}

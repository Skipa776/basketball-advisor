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
        // These must NOT reuse the page's own path. A Razor component route
        // answers every HTTP method, so MapPost("/account/login") is a second
        // candidate for POST /account/login and routing throws
        // AmbiguousMatchException before either handler runs -- which made
        // logging in through the UI impossible while the JSON API kept working.
        endpoints.MapPost("/account/register/submit", RegisterFormAsync)
            .AllowAnonymous()
            .RequireRateLimiting("account")
            .AddEndpointFilter<CookieAntiforgeryFilter>();
        endpoints.MapPost("/account/login/submit", LoginFormAsync)
            .AllowAnonymous()
            .RequireRateLimiting("account")
            .AddEndpointFilter<CookieAntiforgeryFilter>();
        endpoints.MapPost("/account/logout/submit", LogoutFormAsync)
            .RequireAuthorization()
            .AddEndpointFilter<CookieAntiforgeryFilter>();
        // Cast: a Task<IResult> handler taking only HttpContext has the same
        // shape as RequestDelegate, so routing binds it as one and discards the
        // result -- the redirect would never be written.
        endpoints.MapPost(
                "/account/active-league/submit",
                (Delegate)SetActiveLeagueAsync)
            .RequireAuthorization()
            .AddEndpointFilter<CookieAntiforgeryFilter>();
        return endpoints;
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

    public static async Task<IResult> RegisterFormAsync(
        HttpRequest request,
        RegistrationService registrations,
        SignInManager<FantasyUser> signIn,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var result = await registrations.RegisterAsync(
            form["email"].ToString(),
            form["password"].ToString(),
            form["displayName"].ToString(),
            cancellationToken);
        if (result.RegistrationClosed)
        {
            return Results.NotFound();
        }

        if (!result.Succeeded)
        {
            return Results.Redirect("/account/register?error=invalid");
        }

        await signIn.SignInAsync(result.User!, isPersistent: false);
        return Results.Redirect("/");
    }

    public static async Task<IResult> LoginFormAsync(
        HttpRequest request,
        UserManager<FantasyUser> users,
        SignInManager<FantasyUser> signIn,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var user = await users.FindByEmailAsync(form["email"].ToString());
        if (user is null)
        {
            return Results.Redirect("/account/login?error=invalid");
        }

        var result = await signIn.PasswordSignInAsync(
            user,
            form["password"].ToString(),
            form["rememberMe"] == "true",
            lockoutOnFailure: true);
        return result.Succeeded
            ? Results.Redirect("/")
            : Results.Redirect(
                result.IsLockedOut
                    ? "/account/login?error=locked"
                    : "/account/login?error=invalid");
    }

    public static async Task<IResult> LogoutFormAsync(
        SignInManager<FantasyUser> signIn)
    {
        await signIn.SignOutAsync();
        return Results.Redirect("/account/login");
    }

    /// <summary>
    /// Records which league the user is working in. Deliberately does NOT check
    /// that the league exists or belongs to them: the cookie is view state, and
    /// every query that reads it still runs through the ownership filter, so a
    /// value pointing anywhere else simply resolves to nothing. Validating here
    /// would add a database round trip to a click that changes a preference.
    /// </summary>
    public static async Task<IResult> SetActiveLeagueAsync(HttpContext context)
    {
        var form = await context.Request.ReadFormAsync();
        var raw = form["leagueId"].ToString();

        if (Guid.TryParse(raw, out var leagueId))
        {
            context.Response.Cookies.Append(
                ActiveLeague.CookieName,
                leagueId.ToString(),
                ActiveLeague.Options(context.Request.IsHttps));
        }
        else
        {
            context.Response.Cookies.Delete(ActiveLeague.CookieName);
        }

        // Back where they were. Only a same-origin relative path is honoured --
        // a Referer is attacker-controllable, and redirecting to whatever it
        // says turns a preference switch into an open redirect.
        var referer = context.Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var target)
            && string.Equals(
                target.Authority,
                context.Request.Host.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.Redirect(target.PathAndQuery);
        }

        return Results.Redirect("/");
    }
}

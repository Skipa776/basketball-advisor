using FantasyBasketball.Api.Endpoints;
using FantasyBasketball.Api.Middleware;
using FantasyBasketball.Api.Components;

namespace FantasyBasketball.Api;

public static class ApiHost
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddExternalDataHttpClients(builder.Configuration);
    }

    public static void Configure(WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var response = statusCodeContext.HttpContext.Response;
            if (response.HasStarted)
            {
                return;
            }

            var code = response.StatusCode == StatusCodes.Status404NotFound
                ? "not_found"
                : "internal_error";
            var message = response.StatusCode == StatusCodes.Status404NotFound
                ? "The requested resource was not found."
                : "The request could not be completed.";
            await ApiResults.Failure(
                    response.StatusCode,
                    code,
                    message)
                .ExecuteAsync(statusCodeContext.HttpContext);
        });

        app.MapLeagueEndpoints();
        app.MapPlayerEndpoints();
        app.MapImportEndpoints();
        app.MapDraftEndpoints();
        app.MapContextEndpoints();
        app.MapHealthEndpoints();
        app.MapAccountEndpoints();
        app.MapFallback(
            "/api/{**path}",
            () => ApiResults.Failure(
                StatusCodes.Status404NotFound,
                "not_found",
                "The requested resource was not found."));
        app.UseAntiforgery();

        // Static assets are endpoints, so the RequireAuthenticatedUser fallback
        // policy applies to them unless they opt out. Without this the login page
        // -- the first screen a new self-hoster ever sees -- 302s its own
        // stylesheet, theme script, and blazor.web.js to itself and renders
        // completely unstyled with no interactivity. Nothing here is user data:
        // it is the CSS and JS that make the anonymous pages usable at all.
        app.MapStaticAssets(
                "FantasyBasketball.Api.staticwebassets.endpoints.json")
            .AllowAnonymous();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
    }
}

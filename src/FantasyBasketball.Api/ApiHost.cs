using FantasyBasketball.Api.Endpoints;
using FantasyBasketball.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace FantasyBasketball.Api;

public static class ApiHost
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
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

        // Vite replaces fingerprinted files during a rebuild. Serve the current
        // public files before endpoint routing so a running host can load a new
        // bundle that was not in its startup static-asset manifest.
        app.UseStaticFiles();
        app.UseRouting();
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
        app.MapPublicEndpoints();
        // The React app is the only UI. `/` and every `/app` route serve its
        // index; the app itself decides between landing and signed-in pages.
        IResult ReactIndex()
        {
            var index = Path.Combine(app.Environment.WebRootPath, "app", "index.html");
            return File.Exists(index)
                ? Results.File(index, "text/html")
                : Results.NotFound();
        }

        app.MapGet("/", ReactIndex).AllowAnonymous();
        app.MapGet("/app", ReactIndex).AllowAnonymous();
        app.MapGet("/app/{**path}", ReactIndex).AllowAnonymous();
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
        // stylesheet and scripts to itself and renders unstyled. Nothing here is user data:
        // it is the CSS and JS that make the anonymous pages usable at all.
        app.MapStaticAssets(
                "FantasyBasketball.Api.staticwebassets.endpoints.json")
            .AllowAnonymous();
    }
}

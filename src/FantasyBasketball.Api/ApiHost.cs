using FantasyBasketball.Api.Endpoints;
using FantasyBasketball.Api.Middleware;

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
    }
}

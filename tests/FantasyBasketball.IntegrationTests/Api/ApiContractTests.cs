using System.Reflection;
using System.Text.Json;
using FantasyBasketball.Api.Endpoints;
using FantasyBasketball.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed class ApiContractTests
{
    [Fact]
    public async Task A10_unhandled_exception_is_generic_envelope_without_details()
    {
        const string secret = "fixture-connection-string";
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException(
                $"SQL stack trace included {secret}"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        document.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Null);
        document.RootElement.GetProperty("error")
            .GetProperty("code")
            .GetString()
            .ShouldBe("internal_error");
        var body = document.RootElement.GetRawText();
        body.ShouldNotContain(secret);
        body.ShouldNotContain("stack");
        body.ShouldNotContain("SQL");
    }

    [Fact]
    public void A19_every_endpoint_handler_accepts_cancellation_last()
    {
        Type[] endpointTypes =
        [
            typeof(LeagueEndpoints),
            typeof(PlayerEndpoints),
            typeof(ImportEndpoints),
            typeof(DraftEndpoints),
            typeof(ContextEndpoints),
            typeof(HealthEndpoints),
        ];

        var handlers = endpointTypes
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToArray();

        handlers.ShouldNotBeEmpty();
        handlers.ShouldAllBe(method =>
            method.GetParameters().Last().ParameterType
                == typeof(CancellationToken));
    }
}

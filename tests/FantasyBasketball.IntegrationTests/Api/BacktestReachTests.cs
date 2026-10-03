using System.Reflection;
using FantasyBasketball.Api;
using FantasyBasketball.Application.Backtest;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed class BacktestReachTests
{
    [Fact]
    public void B10_no_endpoint_reaches_the_backtest_runner()
    {
        // Minimal-API handlers receive services as parameters, so a handler that could
        // run a backtest would have to take a type from the Backtest namespace.
        var backtestNamespace = typeof(ProjectionBacktestRunner).Namespace;
        var handlers = typeof(ApiHost).Assembly.GetTypes()
            .Where(type => type.Namespace == "FantasyBasketball.Api.Endpoints")
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .ToArray();

        handlers.ShouldNotBeEmpty();
        handlers
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.ParameterType.Namespace == backtestNamespace)
            .ShouldBeEmpty();
    }
}

using FantasyBasketball.Application.Abstractions;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Architecture;

public sealed class RepositoryContractTests
{
    [Fact]
    public void Repository_contracts_point_inward_and_do_not_leak_queryables_or_updates()
    {
        var assembly = typeof(IPlayerRepository).Assembly;
        assembly.GetReferencedAssemblies().Any(reference =>
            reference.Name?.StartsWith(
                "FantasyBasketball.Infrastructure",
                StringComparison.Ordinal) == true).ShouldBeFalse();

        var methods = assembly.GetTypes()
            .Where(type => type.IsInterface
                && type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods())
            .ToArray();

        methods.ShouldNotBeEmpty();
        methods.Any(method => method.Name.Contains("Update", StringComparison.Ordinal))
            .ShouldBeFalse();
        methods.Any(method =>
            method.ReturnType.IsGenericType
            && method.ReturnType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            .ShouldBeFalse();
        methods.All(method =>
            method.GetParameters().Last().ParameterType == typeof(CancellationToken))
            .ShouldBeTrue();
    }
}

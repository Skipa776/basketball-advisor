using System.Net.Http;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.TestSupport;

/// <summary>
/// Terminal handler that fails loudly instead of reaching the network. Install it
/// as the innermost handler wherever a test builds an HTTP pipeline but does not
/// otherwise supply a fixture terminal.
/// </summary>
public sealed class NoNetworkHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            $"A test attempted a real network request to {request.RequestUri}. " +
            "Tests run offline: serve this from a committed fixture.");
}

/// <summary>
/// Row S-12 — no test may make a real network request.
/// </summary>
public sealed class NoNetworkTests
{
    [Fact]
    public async Task S12_the_guard_handler_throws_instead_of_sending()
    {
        using var invoker = new HttpMessageInvoker(new NoNetworkHandler());

        var attempt = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://www.basketball-reference.com/"),
                TestContext.Current.CancellationToken));

        attempt.Message.ShouldContain("real network request");
    }

    /// <summary>
    /// Files that legitimately build an egress-capable handler must say so with this
    /// marker. Today that means driving a loopback Kestrel host started in-process,
    /// which is the app under test rather than a third-party host. Requiring the
    /// marker keeps every exception deliberate and greppable.
    /// </summary>
    private const string LoopbackMarker = "s12-allow: loopback self-host";

    [Fact]
    public void S12_no_test_constructs_an_unmarked_network_handler()
    {
        // The guard above is only worth anything if nothing bypasses it. An
        // egress-capable handler is the bypass, so forbid it at the source level --
        // this is what makes "tests run offline" an enforced property, not a habit.
        // Assembled at runtime so this file does not match its own rule.
        var forbidden = new[] { "new " + "SocketsHttpHandler", "new " + "HttpClientHandler" };

        var offenders = Directory
            .EnumerateFiles(TestPaths.TestsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return forbidden.Any(text.Contains) && !text.Contains(LoopbackMarker);
            })
            .Select(path => Path.GetRelativePath(TestPaths.TestsRoot, path))
            .ToArray();

        offenders.ShouldBeEmpty(
            "these test files construct a network-capable handler without the " +
            $"'{LoopbackMarker}' marker: " + string.Join(", ", offenders));
    }
}

internal static class TestPaths
{
    /// <summary>Walks up from the test binary to the repository's tests/ directory.</summary>
    internal static string TestsRoot { get; } = Resolve();

    private static string Resolve()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.GetFiles("*.sln").Length > 0)
            {
                return Path.Combine(directory.FullName, "tests");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test binary.");
    }
}

using FantasyBasketball.IntegrationTests.TestSupport;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

/// <summary>
/// Framework-neutral interface rows. The Blazor UI and its static component
/// checks were retired 2026-09-23; the React browser journey
/// (src/FantasyBasketball.Web/tests/workspace.mjs, run by HP05) carries the
/// laid-out rows: keyboard pick, no reflow, axe, and narrow-width overflow.
/// </summary>
public sealed class UiInteractionPathTests
{
    private static readonly string RepositoryRoot =
        Directory.GetParent(TestPaths.TestsRoot)!.FullName;

    [Fact]
    public void D31_both_front_doors_name_the_same_product()
    {
        // site/index.html is the GitHub Pages project page and the first thing
        // a stranger sees. A string assertion, not a design judgement: two front
        // doors naming two products is the failure this row exists to catch.
        var projectPage = Path.Combine(RepositoryRoot, "site", "index.html");
        if (!File.Exists(projectPage))
        {
            return;
        }

        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "FantasyBasketball.Web", "src", "Workspace.tsx"))
            .Contains("Fastbreak", StringComparison.Ordinal)
            .ShouldBeTrue("the React app's wordmark should carry the product name");
        File.ReadAllText(projectPage)
            .Contains("Fastbreak", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue("site/index.html is a front door for this product and must name it");
    }

    [Fact]
    public void D34_every_shipped_binary_is_attributed()
    {
        // An unattributed binary is a licensing problem that is invisible until
        // it is expensive.
        var imageRoot = Path.Combine(RepositoryRoot, "src", "FantasyBasketball.Api", "wwwroot", "img");
        if (!Directory.Exists(imageRoot))
        {
            return;
        }

        var manifestPath = Path.Combine(RepositoryRoot, "ASSETS.md");
        File.Exists(manifestPath).ShouldBeTrue("wwwroot/img exists, so ASSETS.md must exist to attribute it");
        var manifest = File.ReadAllText(manifestPath);
        var unattributed = Directory
            .EnumerateFiles(imageRoot, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(file => !manifest.Contains(file!, StringComparison.Ordinal))
            .ToArray();
        unattributed.ShouldBeEmpty(
            "every shipped binary needs an ASSETS.md entry with source, licence, "
            + "author and retrieval date: " + string.Join(", ", unattributed));
    }
}

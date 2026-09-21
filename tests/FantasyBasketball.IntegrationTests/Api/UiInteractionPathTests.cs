using System.Text.RegularExpressions;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

/// <summary>
/// Rows D-28 through D-34: the interaction path, gating R23.
///
/// Every row here failed at least once on the shipped app. Each page was
/// individually correct and the sequence between them was not, which is exactly
/// the class of defect the rest of the design matrix cannot see: it checks
/// components, and these are failures of what joins them.
///
/// D-30 is deliberately absent. It needs a laid-out page and lives in
/// scripts/ui-browser-gate.mjs beside D-14; see context/row_coverage_exceptions.txt.
/// </summary>
public sealed partial class UiDesignTests
{
    // React entry points are HTTP endpoints rather than Razor @page directives.
    // Discover them from their actual route declarations, never an exception list.
    private static IEnumerable<string> HostPageRoutes() =>
        Regex.Matches(File.ReadAllText(Path.Combine(RepositoryRoot, "src", "FantasyBasketball.Api", "ApiHost.cs")),
            @"app\.MapGet\(""(?<route>[^""]+)""")
            .Select(match => match.Groups["route"].Value);

    [Fact]
    public void D28_every_route_is_reachable_from_in_app_navigation()
    {
        // /welcome shipped with no inbound link and a green suite: the route
        // sweep rendered it, so every test passed, and no user could reach it.
        // Rendering a route is not reaching it.
        var routes = PageFiles()
            .SelectMany(path => PageRoute().Matches(File.ReadAllText(path))
                .Select(match => match.Groups["route"].Value))
            .Concat(HostPageRoutes())
            .Where(route => !route.Contains('{', StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Query strings are navigation state, not a different destination:
        // /players?leagueId=... reaches /players.
        var linked = ComponentFiles()
            .SelectMany(path => InternalHref().Matches(File.ReadAllText(path))
                .Select(match => match.Groups["href"].Value))
            .Select(href => href.Split('?', '#')[0])
            .Select(href =>
                href.Length > 1 ? href.TrimEnd('/') : href)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The submit target of the sign-in form. It is a controller endpoint
        // reached by posting the form, never by a link, so a link to it would
        // be the bug.
        string[] postOnly = ["/account/login/submit"];

        var orphans = routes
            .Where(route => !linked.Contains(route))
            .Where(route => !postOnly.Contains(route, StringComparer.OrdinalIgnoreCase))
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();

        orphans.ShouldBeEmpty(
            "every @page route needs an inbound link or nav entry; these are "
            + "orphaned: " + string.Join(", ", orphans));
    }

    [Fact]
    public void D29_anonymous_calls_to_action_resolve_to_a_real_route()
    {
        // The landing CTA and the closed-registration default are each correct
        // on their own; the pair was a dead end on the product's front door.
        //
        // What this row can prove statically is that no anonymously-rendered
        // link points at a route that does not exist. Whether the destination
        // is *available under this instance's configuration* is runtime state,
        // and Register.razor owns it: it renders the closed-registration
        // surface rather than a blank form. That half is asserted by the auth
        // HTTP tests, not here.
        var routes = PageFiles()
            .SelectMany(path => PageRoute().Matches(File.ReadAllText(path))
                .Select(match => match.Groups["route"].Value))
            .Concat(HostPageRoutes())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var anonymousPages = PageFiles()
            .Where(path =>
            {
                var markup = File.ReadAllText(path);
                return markup.Contains("[AllowAnonymous]", StringComparison.Ordinal);
            });

        var broken = anonymousPages
            .SelectMany(path => InternalHref().Matches(File.ReadAllText(path))
                .Select(match => new
                {
                    Page = Path.GetFileName(path),
                    Target = match.Groups["href"].Value.Split('?', '#')[0],
                }))
            .Where(link => link.Target.Length > 1)
            .Select(link => new { link.Page, Target = link.Target.TrimEnd('/') })
            .Where(link => !routes.Contains(link.Target))
            .Select(link => $"{link.Page} -> {link.Target}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        broken.ShouldBeEmpty(
            "an anonymously-rendered link points at a route that does not "
            + "exist: " + string.Join(", ", broken));
    }

    [Fact]
    public void D31_both_front_doors_name_the_same_product()
    {
        // site/index.html is the GitHub Pages project page and the first thing
        // a stranger sees. This is a string assertion, not a design judgement:
        // it cannot tell whether the two pages look related, only that they are
        // not visibly describing two different products.
        var projectPage = Path.Combine(RepositoryRoot, "site", "index.html");
        if (!File.Exists(projectPage))
        {
            return;
        }

        var wordmark = File.ReadAllText(
                Path.Combine(
                    RepositoryRoot,
                    "src",
                    "FantasyBasketball.Api",
                    "Components",
                    "Design",
                    "SiteHeader.razor"))
            .Contains("Fastbreak", StringComparison.Ordinal);

        wordmark.ShouldBeTrue("the app's own header should carry the product name");

        File.ReadAllText(projectPage)
            .Contains("Fastbreak", StringComparison.OrdinalIgnoreCase)
            .ShouldBeTrue(
                "site/index.html is a front door for this product and must name "
                + "it; two front doors naming two products is the failure this "
                + "row exists to catch");
    }

    [Fact]
    public void D32_identifier_inputs_say_where_the_value_comes_from()
    {
        // Context Review asked for a context event ID that no screen printed.
        // A form that demands a value the product never shows is unusable by
        // anyone who did not write it.
        var offenders = new List<string>();

        foreach (var path in PageFiles().Concat(ComponentFiles()).Distinct(StringComparer.Ordinal))
        {
            if (!path.EndsWith(".razor", StringComparison.Ordinal))
            {
                continue;
            }

            var markup = File.ReadAllText(path);
            foreach (Match label in IdentifierLabel().Matches(markup))
            {
                var forId = label.Groups["for"].Value;

                // A picker needs no explanation: the values are on screen.
                var isSelect = Regex.IsMatch(
                    markup,
                    $@"<select[^>]*\bid=""{Regex.Escape(forId)}""",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(2));

                var describes = Regex.IsMatch(
                    markup,
                    $@"<input[^>]*\bid=""{Regex.Escape(forId)}""[^>]*aria-describedby=",
                    RegexOptions.Singleline,
                    TimeSpan.FromSeconds(2));

                if (!isSelect && !describes)
                {
                    offenders.Add($"{Path.GetFileName(path)}#{forId}");
                }
            }
        }

        offenders.ShouldBeEmpty(
            "an input whose label names an identifier must be a picker, or "
            + "describe where the value comes from: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void D33_unbacked_pages_name_their_requirement_and_show_no_numbers()
    {
        // This is what makes a designed shell safe to ship. Without it, "shell"
        // decays into "fabricated demo" in one commit by somebody who wanted a
        // screenshot to look better.
        var shells = PageFiles()
            .Where(path => File.ReadAllText(path)
                .Contains("<NotBuiltState", StringComparison.Ordinal))
            .ToArray();

        shells.ShouldNotBeEmpty(
            "the shell pages should be discoverable by their NotBuiltState");

        foreach (var path in shells)
        {
            var markup = File.ReadAllText(path);
            var name = Path.GetFileName(path);

            markup.ShouldContain(
                "Requirement=\"",
                Case.Sensitive,
                $"{name} must name the requirement or roadmap entry it waits on");

            RequirementValue().Match(markup).Groups["value"].Value
                .ShouldNotBeNullOrWhiteSpace(
                    $"{name}'s Requirement must not be empty");

            // "Coming soon" tells a tester nothing and dates badly. The row is
            // what it is waiting on, in the product's own vocabulary.
            markup.ShouldNotContain(
                "coming soon",
                Case.Insensitive,
                $"{name} must name a requirement, not promise a date");

            // No numeric player or team data. The layout may exist; the numbers
            // may not. [data-numeric] is how this product marks a rendered
            // figure, so its presence on a shell is the tell.
            markup.ShouldNotContain(
                "data-numeric",
                Case.Sensitive,
                $"{name} has no backing capability and must render no figures");
        }
    }

    [Fact]
    public void D34_every_shipped_binary_is_attributed()
    {
        // An unattributed binary is a licensing problem that is invisible until
        // it is expensive. Vacuously green until the first asset lands, which
        // is the point: it is in place before it is needed.
        var imageRoot = Path.Combine(
            RepositoryRoot,
            "src",
            "FantasyBasketball.Api",
            "wwwroot",
            "img");

        if (!Directory.Exists(imageRoot))
        {
            return;
        }

        var manifestPath = Path.Combine(RepositoryRoot, "ASSETS.md");
        File.Exists(manifestPath).ShouldBeTrue(
            "wwwroot/img exists, so ASSETS.md must exist to attribute it");

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

    [GeneratedRegex(@"@page\s+""(?<route>/[^""]*)""")]
    private static partial Regex PageRoute();

    [GeneratedRegex(@"href=""(?<href>/[^""]*)""")]
    private static partial Regex InternalHref();

    // Labels whose text ends in "ID" or "id" as a standalone word -- "League
    // ID", "Affected player ID". Deliberately narrow: it is looking for the
    // shape that failed, not every label in the app.
    [GeneratedRegex(
        @"<label[^>]*\bfor=""(?<for>[^""]+)""[^>]*>[^<]*\bID\s*</label>",
        RegexOptions.IgnoreCase)]
    private static partial Regex IdentifierLabel();

    [GeneratedRegex(@"Requirement=""(?<value>[^""]*)""")]
    private static partial Regex RequirementValue();
}

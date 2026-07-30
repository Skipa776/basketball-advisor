using System.Text.RegularExpressions;
using FantasyBasketball.IntegrationTests.TestSupport;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed partial class UiDesignTests
{
    private static readonly string RepositoryRoot =
        Directory.GetParent(TestPaths.TestsRoot)!.FullName;

    private static readonly string DesignRoot = Path.Combine(
        RepositoryRoot,
        "src",
        "FantasyBasketball.Api",
        "Components",
        "Design");

    [Fact]
    public void D10_components_use_tokens_for_colour_spacing_and_duration()
    {
        var tokenPath = Path.Combine(DesignRoot, "Tokens.razor.css");
        var offenders = ComponentFiles()
            .Where(path => path != tokenPath)
            .Select(path => new
            {
                Path = path,
                Contents = File.ReadAllText(path),
            })
            .Where(file =>
                HexColour().IsMatch(file.Contents)
                || PixelValue().IsMatch(file.Contents)
                || DurationValue().IsMatch(file.Contents))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file.Path))
            .ToArray();

        offenders.ShouldBeEmpty(
            "design values belong in Tokens.razor.css: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void D12_evidence_items_include_symbol_and_text_polarity()
    {
        var markup = File.ReadAllText(
            Path.Combine(DesignRoot, "EvidenceList.razor"));

        markup.ShouldContain("class=\"icon\"");
        markup.ShouldContain("aria-hidden=\"true\"");
        markup.ShouldContain("class=\"polarity\"");
        markup.ShouldContain("(\"+\", \"Support\")");
        markup.ShouldContain("(\"−\", \"Risk\")");
    }

    [Fact]
    public void D11_token_contrast_passes_in_both_themes()
    {
        var stylesheet = File.ReadAllText(
            Path.Combine(DesignRoot, "Tokens.razor.css"));
        var themes = ParseThemeColours(stylesheet);
        var bodyPairs = new[]
        {
            ("color-text", "color-bg"),
            ("color-text", "color-surface"),
            ("color-text-muted", "color-bg"),
            ("color-text-muted", "color-surface"),
            ("color-accent", "color-bg"),
            ("color-accent", "color-surface"),
            ("color-positive", "color-surface"),
            ("color-negative", "color-surface"),
            ("color-caution", "color-surface"),
            ("color-accent-contrast", "color-accent"),
        };
        var boundaryPairs = new[]
        {
            ("color-border-strong", "color-bg"),
            ("color-border-strong", "color-surface"),
        };

        themes.Keys.ShouldBe(["dark", "light"], ignoreOrder: true);
        foreach (var theme in themes)
        {
            foreach (var pair in bodyPairs)
            {
                Contrast(
                        theme.Value[pair.Item1],
                        theme.Value[pair.Item2])
                    .ShouldBeGreaterThanOrEqualTo(
                        4.5,
                        $"{theme.Key} {pair.Item1}/{pair.Item2}");
            }

            foreach (var pair in boundaryPairs)
            {
                Contrast(
                        theme.Value[pair.Item1],
                        theme.Value[pair.Item2])
                    .ShouldBeGreaterThanOrEqualTo(
                        3,
                        $"{theme.Key} {pair.Item1}/{pair.Item2}");
            }
        }
    }

    [Fact]
    public void D13_pick_entry_is_keyboard_operable_and_restores_focus()
    {
        var markup = File.ReadAllText(
            Path.Combine(DesignRoot, "PickEntry.razor"));

        markup.ShouldContain("role=\"combobox\"");
        markup.ShouldContain("args.Key == \"ArrowDown\"");
        markup.ShouldContain("args.Key == \"Enter\"");
        markup.ShouldContain("await OnCommit.InvokeAsync(item)");
        markup.ShouldContain("await Input.FocusAsync()");
        markup.ShouldContain("Undo last pick");
    }

    [Fact]
    public void D14_draft_board_captures_and_restores_measured_row_offsets()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "FantasyBasketball.Api",
            "wwwroot",
            "draft-board.js"));

        script.ShouldContain("getBoundingClientRect()");
        script.ShouldContain("playerId: row.dataset.playerId");
        script.ShouldContain("offset:");
        script.ShouldContain(
            "const offsetDelta = currentOffset - anchor.previous.offset");
        script.ShouldContain("Math.abs(offsetDelta) > 0.01");
        script.ShouldContain("viewport.scrollTop += offsetDelta");
        script.ShouldContain("new MutationObserver");
    }

    [Fact]
    public void D15_reduced_motion_disables_player_row_animation()
    {
        var stylesheet = File.ReadAllText(
            Path.Combine(DesignRoot, "PlayerRow.razor.css"));

        stylesheet.ShouldContain("@media (prefers-reduced-motion: reduce)");
        stylesheet.ShouldContain("transition: none");
    }

    [Fact]
    public void D19_design_components_have_no_service_or_repository_dependency()
    {
        var offenders = DesignFiles()
            .Select(path => new
            {
                Path = path,
                Contents = File.ReadAllText(path),
            })
            .Where(file =>
                file.Contents.Contains("@inject", StringComparison.Ordinal)
                || ServiceDependency().IsMatch(file.Contents)
                || RepositoryDependency().IsMatch(file.Contents))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file.Path))
            .ToArray();

        offenders.ShouldBeEmpty(
            "Components/Design must remain presentational: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void D16_draft_board_has_only_the_two_required_live_regions()
    {
        var page = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "FantasyBasketball.Api",
            "Components",
            "Pages",
            "DraftAssistant.razor"));

        LiveRegion().Matches(page).Count.ShouldBe(2);
        page.ShouldContain("data-live-region=\"pick\"");
        page.ShouldContain("data-live-region=\"top-recommendation\"");
        page.ShouldNotContain("role=\"status\"");
    }

    [Fact]
    public void D17_every_page_composes_a_styled_empty_state()
    {
        var offenders = PageFiles()
            .Where(path =>
                !File.ReadAllText(path)
                    .Contains("<EmptyState", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        offenders.ShouldBeEmpty(
            "every first-run page needs an inventory EmptyState: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void D20_pages_use_inventory_components_and_define_no_stylesheets()
    {
        var pages = PageFiles().ToArray();
        var missingHeader = pages
            .Where(path =>
                !File.ReadAllText(path)
                    .Contains("<PageHeader", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();
        var pageStylesheets = pages
            .Select(path => path + ".css")
            .Where(File.Exists)
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        missingHeader.ShouldBeEmpty(
            "every page must compose the PageHeader inventory component");
        pageStylesheets.ShouldBeEmpty(
            "page styling belongs to scoped inventory or layout components");
    }

    [Fact]
    public void D21_player_table_virtualizes_the_full_pool()
    {
        var markup = File.ReadAllText(
            Path.Combine(DesignRoot, "PlayerTable.razor"));
        var stylesheet = File.ReadAllText(
            Path.Combine(DesignRoot, "PlayerTable.razor.css"));

        markup.ShouldContain("<Virtualize");
        markup.ShouldContain("ItemSize=");
        stylesheet.ShouldContain("max-height:");
        stylesheet.ShouldContain("overflow: auto");
    }

    private static IEnumerable<string> DesignFiles() =>
        Directory.EnumerateFiles(DesignRoot, "*", SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith(".razor", StringComparison.Ordinal)
                || path.EndsWith(".razor.css", StringComparison.Ordinal)
                || path.EndsWith(".cs", StringComparison.Ordinal));

    private static IEnumerable<string> ComponentFiles() =>
        Directory.EnumerateFiles(
                Path.Combine(
                    RepositoryRoot,
                    "src",
                    "FantasyBasketball.Api",
                    "Components"),
                "*",
                SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith(".razor", StringComparison.Ordinal)
                || path.EndsWith(".razor.css", StringComparison.Ordinal));

    private static IEnumerable<string> PageFiles() =>
        Directory.EnumerateFiles(
                Path.Combine(
                    RepositoryRoot,
                    "src",
                    "FantasyBasketball.Api",
                    "Components",
                    "Pages"),
                "*.razor",
                SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path)
                .Contains("@page", StringComparison.Ordinal));

    private static Dictionary<string, Dictionary<string, int[]>> ParseThemeColours(
        string stylesheet)
    {
        var darkBlock = ThemeBlock()
            .Match(stylesheet)
            .Groups["block"]
            .Value;
        var lightBlock = LightThemeBlock()
            .Match(stylesheet)
            .Groups["block"]
            .Value;
        return new Dictionary<string, Dictionary<string, int[]>>
        {
            ["dark"] = ParseColours(darkBlock),
            ["light"] = ParseColours(lightBlock),
        };
    }

    private static Dictionary<string, int[]> ParseColours(string block) =>
        ColourToken().Matches(block)
            .Cast<Match>()
            .ToDictionary(
                match => match.Groups["name"].Value,
                match =>
                {
                    var hex = match.Groups["hex"].Value;
                    return new[]
                    {
                        Convert.ToInt32(hex[..2], 16),
                        Convert.ToInt32(hex[2..4], 16),
                        Convert.ToInt32(hex[4..6], 16),
                    };
                });

    private static double Contrast(int[] foreground, int[] background)
    {
        var foregroundLuminance = Luminance(foreground);
        var backgroundLuminance = Luminance(background);
        return (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05)
            / (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05);
    }

    private static double Luminance(int[] colour) =>
        (0.2126 * Linear(colour[0]))
        + (0.7152 * Linear(colour[1]))
        + (0.0722 * Linear(colour[2]));

    private static double Linear(int channel)
    {
        var value = channel / 255d;
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b")]
    private static partial Regex HexColour();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?px\b")]
    private static partial Regex PixelValue();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?ms\b")]
    private static partial Regex DurationValue();

    [GeneratedRegex(@"\b[A-Za-z0-9_]*Service\b")]
    private static partial Regex ServiceDependency();

    [GeneratedRegex(@"\b[A-Za-z0-9_]*Repository\b")]
    private static partial Regex RepositoryDependency();

    [GeneratedRegex("aria-live=")]
    private static partial Regex LiveRegion();

    [GeneratedRegex(
        @"\.tokens\s*\{(?<block>.*?)\}",
        RegexOptions.Singleline)]
    private static partial Regex ThemeBlock();

    [GeneratedRegex(
        @"\.tokens\[data-theme=""light""\]\s*\{(?<block>.*?)\}",
        RegexOptions.Singleline)]
    private static partial Regex LightThemeBlock();

    [GeneratedRegex(
        @"--(?<name>color-[a-z-]+):\s*#(?<hex>[0-9a-fA-F]{6});")]
    private static partial Regex ColourToken();
}

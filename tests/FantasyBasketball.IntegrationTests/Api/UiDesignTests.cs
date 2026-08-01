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
        // Every surface a foreground can land on, including surface-raised --
        // the recommended row, hover states, popovers, and .button-link all
        // paint their own fill with it, so a boundary drawn on such a control
        // is measured against that fill and not against the page behind it.
        var bodyPairs = BodyPairs;
        var boundaryPairs = BoundaryPairs;

        themes.Keys.ShouldBe(["dark", "light"], ignoreOrder: true);

        // A colour token nobody checks is a contrast failure waiting to be
        // introduced. Decorative-only tokens are exempt by name, never by
        // omission -- adding a token now forces a decision about which it is.
        // --color-surface-wood and --color-court-line carry the shell's hardwood
        // field and the court geometry drawn on it. Decorative by construction:
        // the contract forbids text or an interactive boundary landing on
        // either, which is what keeps them out of a pair.
        string[] decorativeOnly =
            ["color-rule", "color-surface-wood", "color-court-line"];
        var checkedTokens = bodyPairs
            .Concat(boundaryPairs)
            .SelectMany(pair => new[] { pair.Item1, pair.Item2 })
            .Concat(decorativeOnly)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var theme in themes)
        {
            theme.Value.Keys
                .Where(token => !checkedTokens.Contains(token))
                .ShouldBeEmpty(
                    $"{theme.Key} defines colour tokens that no contrast pair "
                    + "covers; add them to a pair or to decorativeOnly");
        }

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
    public void D22_always_dark_island_meets_contrast_in_both_themes()
    {
        var stylesheet = File.ReadAllText(
            Path.Combine(DesignRoot, "Tokens.razor.css"));
        var island = ParseIslandColours(stylesheet);
        var themes = ParseThemeColours(stylesheet);

        island.ShouldNotBeEmpty(
            "the .on-dark island must define its own colour tokens");

        // The island renders the same fill whichever theme is ambient, so it
        // has to be measured on its own rather than inherited. This is the
        // pair that fails without it: the light rank teal on the island's
        // surface is 2.86:1, well under the 4.5 it needs as a number.
        foreach (var pair in BodyPairs)
        {
            Contrast(island[pair.Foreground], island[pair.Surface])
                .ShouldBeGreaterThanOrEqualTo(
                    4.5,
                    $"island {pair.Foreground}/{pair.Surface}");
        }

        foreach (var pair in BoundaryPairs)
        {
            Contrast(island[pair.Foreground], island[pair.Surface])
                .ShouldBeGreaterThanOrEqualTo(
                    3,
                    $"island {pair.Foreground}/{pair.Surface}");
        }

        // It must also actually be dark, in the sense that matters: a light
        // token set copied here would pass the pairs above while rendering an
        // island indistinguishable from the page around it.
        foreach (var token in island.Keys)
        {
            island[token].ShouldBe(
                themes["dark"][token],
                $"island {token} must match the dark set, not the light one");
        }
    }

    [Fact]
    public void D23_only_the_recommended_row_uses_the_accent_on_the_board()
    {
        // Orange is free on the marketing surfaces and reserved on the board.
        // With four minutes on the clock the recommended row has to be the one
        // thing the eye lands on, and an accent spent anywhere else on this
        // surface spends it twice.
        string[] boardStylesheets =
        [
            "DraftBoard.razor.css",
            "PlayerRow.razor.css",
            "PlayerTable.razor.css",
            "StatCell.razor.css",
        ];

        foreach (var file in boardStylesheets)
        {
            var path = Path.Combine(DesignRoot, file);
            if (!File.Exists(path))
            {
                continue;
            }

            // Each comma-separated part is checked on its own, so a rule like
            // ".anything, .recommendation" cannot smuggle an unrelated selector
            // in beside a legitimate one. "take" is allowed because the take
            // marker is the same signal wearing a different element.
            string[] recommendationSignals = ["recommend", "take"];

            foreach (var rule in AccentRule().Matches(File.ReadAllText(path))
                .Cast<Match>())
            {
                foreach (var part in rule.Groups["selector"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries)
                    .Where(part => part.Length > 0))
                {
                    recommendationSignals
                        .Any(signal => part.Contains(
                            signal,
                            StringComparison.OrdinalIgnoreCase))
                        .ShouldBeTrue(
                            $"{file} uses --color-accent in '{part}', which is "
                            + "not the recommended row");
                }
            }
        }
    }

    [Fact]
    public void D24_carousel_pauses_and_does_not_move_under_reduced_motion()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "FantasyBasketball.Api",
            "wwwroot",
            "carousel.js"));

        // WCAG 2.2.2: anything that moves for more than five seconds needs a
        // way to stop it. A CSS media query cannot stop a timer, so the
        // reduced-motion decision has to live in the script.
        script.ShouldContain("prefers-reduced-motion: reduce");
        script.ShouldContain("if (reduceMotion() || paused || hovered)");
        script.ShouldContain("clearInterval");
        script.ShouldContain("mouseenter");
        script.ShouldContain("mouseleave");
        script.ShouldContain("focusin");
        script.ShouldContain("focusout");

        var markup = File.ReadAllText(
            Path.Combine(DesignRoot, "RiserCarousel.razor"));
        markup.ShouldContain("data-carousel-toggle");
        markup.ShouldContain("aria-pressed");
        markup.ShouldContain("aria-roledescription=\"carousel\"");
        markup.ShouldContain("aria-roledescription=\"slide\"");

        // An auto-advancing live region interrupts a screen reader mid
        // sentence, so slide changes must not be announced.
        LiveRegion().IsMatch(markup).ShouldBeFalse(
            "carousel slides must not announce themselves as they rotate");
    }

    [Fact]
    public void D27_numbers_never_render_in_the_display_face()
    {
        // Anton is condensed and not tabular. Every number on this board is
        // read as a column, and a display face destroys that alignment.
        foreach (var path in ComponentFiles()
            .Where(path => path.EndsWith(".razor.css", StringComparison.Ordinal)))
        {
            foreach (var rule in DisplayFontRule().Matches(File.ReadAllText(path))
                .Cast<Match>())
            {
                var selector = rule.Groups["selector"].Value;
                foreach (var numeric in new[] { "data-numeric", ".rank", ".value", ".stat" })
                {
                    selector.Contains(numeric, StringComparison.Ordinal)
                        .ShouldBeFalse(
                            $"{Path.GetFileName(path)} applies --font-display to "
                            + $"'{selector.Trim()}', which carries numbers");
                }
            }
        }

        // And the numeric helper still pins the mono face for every page.
        var layout = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "FantasyBasketball.Api",
            "Components",
            "Layout",
            "MainLayout.razor.css"));
        layout.ShouldContain("[data-numeric]");
        layout.ShouldContain("font-family: var(--font-mono)");
        layout.ShouldContain("font-variant-numeric: tabular-nums");
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
    public void D15_board_rows_declare_no_motion_to_reduce()
    {
        // The board holds still by scroll-anchoring in draft-board.js, not by
        // animating rows into their new rank, so re-rank is already instant
        // under every motion preference. This asserts that stays true. A
        // geometry transition added to a row would both fight the anchor
        // correction and reintroduce movement that nothing turns off -- and
        // the old assertion (a reduced-motion block exists) could not tell the
        // difference, because it passed while guarding a transition that
        // animated nothing.
        string[] movementProperties =
            ["transform", "translate", "top", "inset", "margin", "height"];
        var stylesheet = File.ReadAllText(
            Path.Combine(DesignRoot, "PlayerRow.razor.css"));

        foreach (var declaration in TransitionValue()
            .Matches(stylesheet)
            .Cast<Match>())
        {
            var value = declaration.Groups["value"].Value;
            foreach (var property in movementProperties)
            {
                value.Contains(property, StringComparison.Ordinal)
                    .ShouldBeFalse(
                        $"a board row must not transition '{property}'");
            }
        }

        // The anchor correction assigns scrollTop outright. A smooth scroll or
        // a Web Animations call here would animate the very displacement D-14
        // exists to cancel.
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "FantasyBasketball.Api",
            "wwwroot",
            "draft-board.js"));

        script.ShouldNotContain("behavior");
        script.ShouldNotContain(".animate(");
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
        // The rule is that nothing renders as an unstyled blank screen, not
        // that one particular component appears. AuthPanel counts: it is the
        // styled first-run surface for the auth pages, which previously
        // satisfied this row by wrapping a login form in an EmptyState -- an
        // abuse of that component that happened to pass a substring scan.
        // NotBuiltState joins them for the same reason: a page whose capability
        // does not exist has no data state to reach, and its whole surface is a
        // styled account of why. Row D-33 is what stops that being a loophole --
        // it makes the shell name its requirement and render no numbers.
        string[] firstRunSurfaces = ["<EmptyState", "<AuthPanel", "<NotBuiltState"];

        // Named, never silent. A page here renders content in every state, so
        // it has no empty state to style; adding one would be decoration for a
        // case that cannot occur.
        string[] alwaysPopulated = ["Welcome.razor"];

        var offenders = PageFiles()
            .Where(path => !alwaysPopulated.Any(
                name => path.EndsWith(name, StringComparison.Ordinal)))
            .Where(path =>
            {
                var markup = File.ReadAllText(path);
                return !firstRunSurfaces.Any(
                    surface => markup.Contains(surface, StringComparison.Ordinal));
            })
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        offenders.ShouldBeEmpty(
            "every first-run page needs an inventory EmptyState, AuthPanel, or "
            + "NotBuiltState: " + string.Join(", ", offenders));
    }

    [Fact]
    public void D20_pages_use_inventory_components_and_define_no_stylesheets()
    {
        var pages = PageFiles().ToArray();
        // Either inventory component supplies the page's heading. AuthPanel
        // renders its own h1, so requiring PageHeader as well would put two
        // level-one headings on the login page.
        string[] headings = ["<PageHeader", "<AuthPanel"];
        var missingHeader = pages
            .Where(path =>
            {
                var markup = File.ReadAllText(path);
                return !headings.Any(
                    heading => markup.Contains(heading, StringComparison.Ordinal));
            })
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

    // Light is the base block now; dark is the override. The island is a third
    // set that belongs to neither theme -- see D22.
    private static Dictionary<string, Dictionary<string, int[]>> ParseThemeColours(
        string stylesheet) =>
        new()
        {
            ["light"] = ParseColours(
                LightThemeBlock().Match(stylesheet).Groups["block"].Value),
            ["dark"] = ParseColours(
                DarkThemeBlock().Match(stylesheet).Groups["block"].Value),
        };

    private static Dictionary<string, int[]> ParseIslandColours(
        string stylesheet) =>
        ParseColours(IslandBlock().Match(stylesheet).Groups["block"].Value);

    private static string[] Surfaces =>
        ["color-bg", "color-surface", "color-surface-raised"];

    // 4.5:1 -- these can all carry body-sized text. --color-accent is
    // deliberately absent: it is a fill and display-type colour, and the
    // boldest orange that clears 4.5 on off-white is too dull to be a brand.
    // --color-accent-ink exists for the cases where orange must be small text.
    private static (string Foreground, string Surface)[] BodyPairs =>
    [
        .. new[]
            {
                "color-text",
                "color-text-muted",
                "color-accent-ink",
                "color-rank",
                "color-positive",
                "color-negative",
                "color-caution",
            }
            .SelectMany(foreground => Surfaces.Select(
                surface => (foreground, surface))),
        ("color-accent-contrast", "color-accent"),
    ];

    // 3:1 -- UI boundaries and large display type only.
    private static (string Foreground, string Surface)[] BoundaryPairs =>
    [
        .. Surfaces.Select(surface => ("color-border-strong", surface)),
        .. Surfaces.Select(surface => ("color-accent", surface)),
    ];

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
    private static partial Regex LightThemeBlock();

    [GeneratedRegex(
        @"\.tokens\[data-theme=""dark""\]\s*\{(?<block>.*?)\}",
        RegexOptions.Singleline)]
    private static partial Regex DarkThemeBlock();

    [GeneratedRegex(
        @"\.tokens ::deep \.on-dark\s*\{(?<block>.*?)\}",
        RegexOptions.Singleline)]
    private static partial Regex IslandBlock();

    [GeneratedRegex(
        @"--(?<name>color-[a-z-]+):\s*#(?<hex>[0-9a-fA-F]{6});")]
    private static partial Regex ColourToken();

    [GeneratedRegex(@"transition:\s*(?<value>[^;]*);")]
    private static partial Regex TransitionValue();

    [GeneratedRegex(
        @"(?<selector>[^{}]+)\{[^{}]*var\(--color-accent\)[^{}]*\}",
        RegexOptions.Singleline)]
    private static partial Regex AccentRule();

    [GeneratedRegex(
        @"(?<selector>[^{}]+)\{[^{}]*var\(--font-display\)[^{}]*\}",
        RegexOptions.Singleline)]
    private static partial Regex DisplayFontRule();
}

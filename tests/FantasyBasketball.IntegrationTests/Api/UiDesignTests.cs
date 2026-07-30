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
        var offenders = DesignFiles()
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
}

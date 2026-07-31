namespace FantasyBasketball.Api.Components.Design;

public sealed class DemoContentOptions
{
    public const string SectionName = "Demo";

    /// <summary>
    /// Off by default. A self-hosted instance shows its real data or an empty
    /// state; sample content is opt-in, and everything it produces is labelled
    /// fictional wherever it renders (row A-26).
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Fictional players, teams and headlines for the landing page. These names are
/// invented: no real player's statistics, likeness, or reporting is reproduced
/// here, and nothing in this file is derived from a data provider.
/// </summary>
public static class DemoContent
{
    public static IReadOnlyList<ImportPlatformItem> Platforms { get; } =
    [
        new("ESPN", "CSV export or manual entry"),
        new("Yahoo", "Read-only connection, planned"),
        new("Sleeper", "Public read-only endpoints, planned"),
    ];

    public static IReadOnlyList<OnboardingStepItem> SetupSteps { get; } =
    [
        new("Choose a platform", "Tells the next step which scoring shapes to offer."),
        new("Set scoring", "Points, categories, or a Sleeper variant."),
        new("Import players", "Fills the board so recommendations have something to rank."),
    ];

    public static IReadOnlyList<MoverBoard> Movers { get; } =
    [
        new("Points league movers", "Points",
        [
            new("Dario Vance", "POR", "PG", 14, 31, MoveDirection.Riser,
                "Starting point guard minutes since the trade deadline."),
            new("Emeka Baptiste", "SAC", "C", 22, 38, MoveDirection.Riser,
                "Usage up with the starting centre out."),
            new("Rhys Calloway", "ORL", "SG", 47, 26, MoveDirection.Faller,
                "Moved to the bench; minutes down nine per game."),
            new("Tobias Lindqvist", "MIA", "PF", 61, 40, MoveDirection.Faller,
                "Minutes restriction on return from injury."),
        ]),
        new("Category league movers", "Categories",
        [
            new("Emeka Baptiste", "SAC", "C", 9, 19, MoveDirection.Riser,
                "Blocks and rebounds carry more weight in nine-category."),
            new("Jonas Adeyemi", "UTA", "SF", 18, 29, MoveDirection.Riser,
                "Steals rate holding through a larger role."),
            new("Rhys Calloway", "ORL", "SG", 52, 33, MoveDirection.Faller,
                "Three-point volume fell with the bench role."),
            new("Marcus Oyelaran", "CHA", "PG", 44, 27, MoveDirection.Faller,
                "Turnovers up sharply as the primary handler."),
        ]),
    ];

    public static IReadOnlyList<RankedPlayerItem> Rankings { get; } =
    [
        new(1, "Dario Vance", "POR", "PG", 48.6m),
        new(2, "Emeka Baptiste", "SAC", "C", 46.2m),
        new(3, "Jonas Adeyemi", "UTA", "SF", 44.9m),
        new(4, "Tobias Lindqvist", "MIA", "PF", 43.1m),
        new(5, "Marcus Oyelaran", "CHA", "PG", 41.7m),
        new(6, "Rhys Calloway", "ORL", "SG", 40.3m),
        new(7, "Anton Pereira", "DET", "SG", 39.5m),
        new(8, "Kofi Danquah", "IND", "C", 38.8m),
    ];

    public static IReadOnlyList<CoverageItem> Coverage { get; } =
    [
        new("Vance moved to the starting lineup",
            "Example Wire", "2h ago",
            "A role change is the kind of event that moves a projection before any box score does."),
        new("Baptiste absorbing usage with Danquah out",
            "Example Beat", "6h ago",
            "Short-sample usage spikes are flagged as low confidence until the minutes hold."),
        new("Calloway to the bench after the trade",
            "Example Daily", "1d ago",
            "Proposed context events wait for human review before they can affect trusted output."),
    ];
}

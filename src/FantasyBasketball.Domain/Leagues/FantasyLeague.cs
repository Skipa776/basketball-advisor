using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Leagues;

public sealed record FantasyLeague
{
    public FantasyLeague(
        Guid id,
        string name,
        LeagueType type,
        int teamCount,
        IReadOnlyList<ScoringRule> scoringRules,
        IReadOnlyList<StatKey> categories,
        IReadOnlyList<RosterSlot> rosterSlots,
        LineupCadence cadence,
        int weeklyAcquisitionLimit = DefaultWeeklyAcquisitionLimit)
    {
        if (weeklyAcquisitionLimit is < 1 or > MaximumWeeklyAcquisitionLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weeklyAcquisitionLimit),
                $"Weekly acquisitions must be between 1 and {MaximumWeeklyAcquisitionLimit}.");
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(scoringRules);
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(rosterSlots);

        if (teamCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(teamCount),
                "TeamCount must be greater than zero.");
        }

        if (rosterSlots.Count == 0)
        {
            throw new ArgumentException(
                "RosterSlots requires at least one slot.",
                nameof(rosterSlots));
        }

        ValidateScoring(type, scoringRules, categories);

        Id = id;
        Name = name.Trim();
        Type = type;
        TeamCount = teamCount;
        ScoringRules = Copy(scoringRules);
        Categories = Copy(categories);
        RosterSlots = Copy(rosterSlots);
        Cadence = cadence;
        WeeklyAcquisitionLimit = weeklyAcquisitionLimit;
    }

    /// <summary>ESPN's common default; owner-approved 2026-09-23.</summary>
    public const int DefaultWeeklyAcquisitionLimit = 7;

    public const int MaximumWeeklyAcquisitionLimit = 99;

    /// <summary>Add/drops allowed per matchup week; streaming plans never exceed it.</summary>
    public int WeeklyAcquisitionLimit { get; }

    public Guid Id { get; }

    public string Name { get; }

    public LeagueType Type { get; }

    public int TeamCount { get; }

    public IReadOnlyList<ScoringRule> ScoringRules { get; }

    public IReadOnlyList<StatKey> Categories { get; }

    public IReadOnlyList<RosterSlot> RosterSlots { get; }

    public LineupCadence Cadence { get; }

    private static void ValidateScoring(
        LeagueType type,
        IReadOnlyList<ScoringRule> scoringRules,
        IReadOnlyList<StatKey> categories)
    {
        if (type == LeagueType.Points && scoringRules.Count == 0)
        {
            throw new ArgumentException(
                "ScoringRules requires at least one rule for a points league.",
                nameof(scoringRules));
        }

        if (type == LeagueType.Categories && categories.Count == 0)
        {
            throw new ArgumentException(
                "Categories requires at least one category for a category league.",
                nameof(categories));
        }

        var duplicateRule = scoringRules
            .GroupBy(rule => rule.Stat)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRule is not null)
        {
            throw new ArgumentException(
                $"ScoringRules contains duplicate stat {duplicateRule.Key}.",
                nameof(scoringRules));
        }

        var duplicateCategory = categories
            .GroupBy(category => category)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCategory is not null)
        {
            throw new ArgumentException(
                $"Categories contains duplicate stat {duplicateCategory.Key}.",
                nameof(categories));
        }
    }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}

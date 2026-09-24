using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Common;

/// <summary>
/// An injury report describes the day it was fetched. It is applied only to a "today" it is
/// current for; an old report, or today's report against a replayed past season, is shown as
/// not applied rather than silently used (data_integrity_policy rule 5).
/// </summary>
public static class CurrentAvailability
{
    public const int FreshDays = 2;

    public static bool Applies(IReadOnlyDictionary<PlayerId, PlayerAvailability> reports, DateOnly today) =>
        Fetched(reports) is { } fetched && fetched >= today.AddDays(-FreshDays) && fetched <= today.AddDays(1);

    public static bool MissesGames(IReadOnlyDictionary<PlayerId, PlayerAvailability> reports, PlayerId player, DateOnly today) =>
        Applies(reports, today) && reports.TryGetValue(player, out var report) && report.MissesGames;

    public static string? Label(IReadOnlyDictionary<PlayerId, PlayerAvailability> reports, PlayerId player) =>
        reports.TryGetValue(player, out var report) ? report.Label : null;

    public static string Note(IReadOnlyDictionary<PlayerId, PlayerAvailability> reports, DateOnly today)
    {
        if (Fetched(reports) is not { } fetched)
        {
            return "No injury report has been imported, so every scheduled game counts.";
        }

        var source = reports.Values.First().Provenance.Source;
        return Applies(reports, today)
            ? $"Injury report from {source}, fetched {fetched:MMM d}: out, IR and suspended players' games do not count."
            : $"The {source} injury report is from {fetched:MMM d, yyyy}, so it is not applied to {today:MMM d, yyyy}; every scheduled game counts.";
    }

    private static DateOnly? Fetched(IReadOnlyDictionary<PlayerId, PlayerAvailability> reports) =>
        reports.Count == 0 ? null : DateOnly.FromDateTime(reports.Values.Max(report => report.Provenance.FetchedAt).UtcDateTime);
}

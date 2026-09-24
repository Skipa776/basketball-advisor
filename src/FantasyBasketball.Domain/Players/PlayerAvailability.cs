using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Domain.Players;

public enum AvailabilityStatus
{
    Out,
    InjuredReserve,
    Suspended,
    Doubtful,
    Questionable,
    DayToDay,
    Other,
}

/// <summary>
/// A provider's current injury report for one player. Reference data with provenance, never a
/// verified context event: it sets the streaming contract's availability (0 when out) and never
/// changes a projection.
/// </summary>
public sealed record PlayerAvailability(
    PlayerId PlayerId,
    AvailabilityStatus Status,
    string? BodyPart,
    string? Notes,
    DateTimeOffset? ReportedAt,
    DataProvenance Provenance)
{
    /// <summary>Out, IR and suspended players' games do not count; the rest are flagged only.</summary>
    public bool MissesGames => Status is AvailabilityStatus.Out or AvailabilityStatus.InjuredReserve or AvailabilityStatus.Suspended;

    public string Label => (Status switch
    {
        AvailabilityStatus.InjuredReserve => "IR",
        AvailabilityStatus.DayToDay => "Day-to-day",
        _ => Status.ToString(),
    }) + (string.IsNullOrWhiteSpace(BodyPart) ? string.Empty : $" ({BodyPart})");

    /// <summary>Provider codes seen in Sleeper's NBA player map (Out, IR, DTD) plus common variants.</summary>
    public static AvailabilityStatus? ParseStatus(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "OUT" or "O" => AvailabilityStatus.Out,
        "IR" or "INJ RES" or "INJURED RESERVE" => AvailabilityStatus.InjuredReserve,
        "SUS" or "SUSP" or "SUSPENSION" or "SUSPENDED" => AvailabilityStatus.Suspended,
        "D" or "DOUBTFUL" => AvailabilityStatus.Doubtful,
        "Q" or "QUESTIONABLE" or "GTD" => AvailabilityStatus.Questionable,
        "DTD" or "DAY TO DAY" or "DAY-TO-DAY" => AvailabilityStatus.DayToDay,
        _ => AvailabilityStatus.Other,
    };
}

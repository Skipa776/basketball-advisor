namespace FantasyBasketball.Infrastructure.Workers;

public sealed class RefreshWorkerOptions
{
    public const string SectionName = "RefreshWorkers";

    /// <summary>
    /// Whether the recurring refreshers run at all. True by default: an
    /// unattended instance should keep itself fresh.
    ///
    /// Turning them off frees the whole per-host rate budget for a manual
    /// import. Each worker fires once on startup and then daily, so they are
    /// not a constant drain -- but every restart is a fresh volley, and on
    /// balldontlie's free tier a handful of those plus a paginating player
    /// import is enough to earn a 429 against a perfectly valid key.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public ScheduleRefreshOptions Schedule { get; set; } = new();

    public StatRefreshOptions Stats { get; set; } = new();

    public AdpRefreshOptions Adp { get; set; } = new();

    public bool IsValid() =>
        Schedule.IsValid()
        && Stats.IsValid()
        && Adp.IsValid();
}

public sealed class ScheduleRefreshOptions
{
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan Cadence { get; set; } = TimeSpan.FromDays(1);

    public int LookbackDays { get; set; } = 1;

    public int LookAheadDays { get; set; } = 14;

    public bool IsValid() =>
        StartupDelay >= TimeSpan.Zero
        && Cadence > TimeSpan.Zero
        && LookbackDays >= 0
        && LookAheadDays >= 0;
}

public sealed class StatRefreshOptions
{
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan Cadence { get; set; } = TimeSpan.FromDays(1);

    public bool IsValid() =>
        StartupDelay >= TimeSpan.Zero
        && Cadence > TimeSpan.Zero;
}

public sealed class AdpRefreshOptions
{
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan Cadence { get; set; } = TimeSpan.FromDays(1);

    public int ActiveFromMonth { get; set; } = 7;

    public int ActiveThroughMonth { get; set; } = 10;

    public bool IsValid() =>
        StartupDelay >= TimeSpan.Zero
        && Cadence > TimeSpan.Zero
        && ActiveFromMonth is >= 1 and <= 12
        && ActiveThroughMonth is >= 1 and <= 12
        && ActiveFromMonth <= ActiveThroughMonth;
}

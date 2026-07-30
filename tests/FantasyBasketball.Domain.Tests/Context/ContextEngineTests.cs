using System.Text.Json;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Context;

public sealed class ContextEngineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void C01_new_events_are_proposed_and_only_explicit_human_action_verifies()
    {
        var contextEvent = CreateEvent();

        contextEvent.Verification.ShouldBe(VerificationState.Proposed);

        contextEvent.VerifyByHuman(Guid.NewGuid(), Now);

        contextEvent.Verification.ShouldBe(VerificationState.Verified);
        contextEvent.VerifiedByUserId.ShouldNotBeNull();
        contextEvent.VerifiedAt.ShouldBe(Now);
    }

    [Fact]
    public void C02_proposed_event_applies_with_reduced_and_visible_confidence()
    {
        var contextEvent = CreateEvent();
        var impact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            contextEvent.PrimaryPlayerId!.Value);

        var adjusted = Apply([contextEvent], [impact]);

        adjusted.AppliedContextEventIds.ShouldContain(contextEvent.Id);
        adjusted.HasUnverifiedContext.ShouldBeTrue();
        adjusted.ContextCertainty.ShouldBe(0.5m);
        adjusted.ProjectedPerGame[StatKey.PTS].ShouldBeGreaterThan(
            Baseline().ProjectedPerGame[StatKey.PTS]);
    }

    [Fact]
    public void C03_rejected_event_is_retained_but_never_applies()
    {
        var contextEvent = CreateEvent();
        contextEvent.RejectByHuman(Guid.NewGuid(), Now);
        var impact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            contextEvent.PrimaryPlayerId!.Value);

        var adjusted = Apply([contextEvent], [impact]);

        contextEvent.Verification.ShouldBe(VerificationState.Rejected);
        adjusted.AppliedContextEventIds.ShouldBeEmpty();
        adjusted.ProjectedPerGame.Values.ShouldBe(Baseline().ProjectedPerGame.Values);
    }

    [Fact]
    public void C04_future_event_does_not_apply()
    {
        var contextEvent = CreateEvent(effectiveFrom: Now.AddMinutes(1));
        var impact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            contextEvent.PrimaryPlayerId!.Value);

        Apply([contextEvent], [impact]).AppliedContextEventIds.ShouldBeEmpty();
    }

    [Fact]
    public void C05_override_replaces_defaults_and_records_override()
    {
        var contextEvent = CreateEvent(magnitude: 1m);
        var defaultImpact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            contextEvent.PrimaryPlayerId!.Value);

        var overridden = defaultImpact.Override(
            minutesDelta: 1m,
            usageDelta: 0m,
            assistShareDelta: 0m,
            reboundShareDelta: 0m,
            shotVolumeDelta: 0m,
            roleRiskDelta: 0.05m,
            projectionConfidenceDelta: -0.10m);

        defaultImpact.MinutesDelta.ShouldBe(6m);
        defaultImpact.UsageDelta.ShouldBe(0.10m);
        overridden.MinutesDelta.ShouldBe(1m);
        overridden.UsageDelta.ShouldBe(0m);
        overridden.IsOverridden.ShouldBeTrue();
    }

    [Fact]
    public void C06_event_without_source_is_rejected_and_manual_source_is_recorded()
    {
        Should.Throw<ArgumentException>(() => ContextEvent.Create(
            Guid.NewGuid(),
            ContextEventType.RotationChange,
            null,
            Player,
            [Player],
            Now,
            Now,
            ContextDirection.Positive,
            0.5m,
            Confidence.Moderate,
            null,
            string.Empty,
            null,
            "Rotation expanded"));

        CreateEvent().SourceName.ShouldBe("manual");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    public void C07_magnitude_is_clamped_on_write(decimal supplied, decimal expected)
    {
        CreateEvent(magnitude: supplied).Magnitude.ShouldBe(expected);
    }

    [Fact]
    public void Catalog_defines_impact_and_expiration_for_every_event_type()
    {
        foreach (var eventType in Enum.GetValues<ContextEventType>())
        {
            var defaults = ContextEventCatalog.GetDefaults(eventType);

            defaults.ShouldNotBeNull();
            if (eventType == ContextEventType.Injury)
            {
                defaults.DefaultExpiration.ShouldBeNull();
            }
            else
            {
                defaults.DefaultExpiration.ShouldNotBeNull();
            }
        }
    }

    [Fact]
    public void P05_applying_and_removing_context_never_mutates_baseline()
    {
        var baseline = Baseline();
        var before = JsonSerializer.SerializeToUtf8Bytes(baseline);
        var contextEvent = CreateEvent();
        var impact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            Player);

        var first = Apply(baseline, [contextEvent], [impact]);
        var removed = Apply(baseline, [], []);

        first.Id.ShouldNotBe(removed.Id);
        JsonSerializer.SerializeToUtf8Bytes(baseline).ShouldBe(before);
    }

    [Fact]
    public void P06_usage_and_shot_volume_choose_larger_absolute_delta()
    {
        var contextEvent = CreateEvent();
        var impact = PlayerContextImpact.CreateOverride(
            Guid.NewGuid(),
            contextEvent.Id,
            Player,
            0m,
            0.20m,
            0m,
            0m,
            -0.30m,
            0m,
            0m);

        var adjusted = Apply([contextEvent], [impact]);

        adjusted.ProjectedPerGame[StatKey.PTS].ShouldBe(7m);
        adjusted.ProjectedPerGame[StatKey.FGA].ShouldBe(5.6m);
    }

    [Fact]
    public void P07_stacked_events_clamp_rate_multipliers_and_minutes()
    {
        var events = Enumerable.Range(0, 5)
            .Select(_ => CreateEvent(magnitude: 1m))
            .ToArray();
        var impacts = events.Select(contextEvent =>
            PlayerContextImpact.CreateOverride(
                Guid.NewGuid(),
                contextEvent.Id,
                Player,
                20m,
                0.4m,
                0.4m,
                0.4m,
                0m,
                0.4m,
                0m)).ToArray();

        var adjusted = Apply(events, impacts);

        adjusted.ProjectedPerGame[StatKey.MIN].ShouldBe(42m);
        adjusted.ProjectedPerGame[StatKey.PTS].ShouldBe(31.5m);
        adjusted.ProjectedPerGame[StatKey.AST].ShouldBe(6.3m);
        adjusted.RoleRisk.ShouldBe(1m);
    }

    [Fact]
    public void P08_expired_event_is_excluded_at_computation_time()
    {
        var contextEvent = CreateEvent(expectedExpiration: Now);
        var impact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            Player);

        Apply([contextEvent], [impact]).AppliedContextEventIds.ShouldBeEmpty();
    }

    [Fact]
    public void P09_adjusted_projection_names_baseline_and_applied_events()
    {
        var contextEvent = CreateEvent();
        var adjusted = Apply(
            [contextEvent],
            [PlayerContextImpact.CreateDefault(
                Guid.NewGuid(),
                contextEvent,
                Player)]);

        adjusted.BaselineProjectionId.ShouldBe(BaselineId);
        adjusted.AppliedContextEventIds.ShouldBe([contextEvent.Id]);
    }

    private static readonly PlayerId Player = new(Guid.Parse(
        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private static readonly Guid BaselineId = Guid.Parse(
        "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static ContextEvent CreateEvent(
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? expectedExpiration = null,
        decimal magnitude = 1m) =>
        ContextEvent.Create(
            Guid.NewGuid(),
            ContextEventType.StartingLineupChange,
            null,
            Player,
            [Player],
            Now,
            effectiveFrom ?? Now,
            ContextDirection.Positive,
            magnitude,
            Confidence.High,
            null,
            "manual",
            null,
            "Moved into the starting lineup",
            expectedExpiration);

    private static AdjustedProjection Apply(
        IReadOnlyList<ContextEvent> events,
        IReadOnlyList<PlayerContextImpact> impacts) =>
        Apply(Baseline(), events, impacts);

    private static AdjustedProjection Apply(
        BaselineProjection baseline,
        IReadOnlyList<ContextEvent> events,
        IReadOnlyList<PlayerContextImpact> impacts) =>
        new ContextApplier(new ConfidenceCalculator()).Apply(
            Guid.NewGuid(),
            baseline,
            events,
            impacts,
            Now);

    private static BaselineProjection Baseline() =>
        new(
            BaselineId,
            Player,
            20m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 1m,
                [StatKey.PTS] = 0.5m,
                [StatKey.FGM] = 0.2m,
                [StatKey.FGA] = 0.4m,
                [StatKey.FG3M] = 0.1m,
                [StatKey.FG3A] = 0.2m,
                [StatKey.FTM] = 0.1m,
                [StatKey.FTA] = 0.1m,
                [StatKey.AST] = 0.1m,
                [StatKey.REB] = 0.2m,
                [StatKey.OREB] = 0.05m,
                [StatKey.DREB] = 0.15m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 20m,
                [StatKey.PTS] = 10m,
                [StatKey.FGM] = 4m,
                [StatKey.FGA] = 8m,
                [StatKey.FG3M] = 2m,
                [StatKey.FG3A] = 4m,
                [StatKey.FTM] = 2m,
                [StatKey.FTA] = 2m,
                [StatKey.AST] = 2m,
                [StatKey.REB] = 4m,
                [StatKey.OREB] = 1m,
                [StatKey.DREB] = 3m,
            }),
            70,
            Now,
            "context-test-v1");
}

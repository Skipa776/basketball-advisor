using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Context;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Context;

public sealed class ContextEventServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_verify_override_and_expire_are_human_audited()
    {
        var repository = new FakeContextEventRepository();
        var service = new ContextEventService(repository, new FixedTimeProvider(Now));
        var userId = Guid.NewGuid();
        var playerId = new PlayerId(Guid.NewGuid());
        var contextEvent = ContextEvent.Create(
            Guid.NewGuid(),
            ContextEventType.RotationChange,
            null,
            playerId,
            [playerId],
            Now,
            Now,
            ContextDirection.Positive,
            1m,
            Confidence.High,
            null,
            "manual",
            null,
            "Rotation expanded");
        var impact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            playerId);

        await service.CreateAsync(
            contextEvent,
            [impact],
            TestContext.Current.CancellationToken);
        await service.VerifyAsync(
            contextEvent.Id,
            userId,
            TestContext.Current.CancellationToken);
        await service.OverrideImpactAsync(
            contextEvent.Id,
            playerId,
            new ContextImpactOverride(1m, 0m, 0m, 0m, 0m, 0.1m, -0.1m),
            userId,
            TestContext.Current.CancellationToken);
        await service.ExpireAsync(
            contextEvent.Id,
            userId,
            TestContext.Current.CancellationToken);
        repository.Event!.Verification.ShouldBe(VerificationState.Verified);
        repository.Event.ReviewedByUserId.ShouldBe(userId);
        repository.Event.ExpectedExpiration.ShouldBe(Now);
        repository.Impact!.IsOverridden.ShouldBeTrue();
        repository.Impact.MinutesDelta.ShouldBe(1m);
        repository.ImpactOverrideUserId.ShouldBe(userId);
    }

    [Fact]
    public async Task Missing_event_or_impact_is_rejected()
    {
        var service = new ContextEventService(
            new FakeContextEventRepository(),
            new FixedTimeProvider(Now));

        await Should.ThrowAsync<KeyNotFoundException>(() => service.VerifyAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken));
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            service.OverrideImpactAsync(
                Guid.NewGuid(),
                new PlayerId(Guid.NewGuid()),
                new ContextImpactOverride(0m, 0m, 0m, 0m, 0m, 0m, 0m),
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A13_already_verified_or_rejected_event_conflicts()
    {
        var repository = new FakeContextEventRepository();
        var service = new ContextEventService(repository, new FixedTimeProvider(Now));
        var playerId = new PlayerId(Guid.NewGuid());
        var first = ContextEvent.Create(
            Guid.NewGuid(),
            ContextEventType.RotationChange,
            null,
            playerId,
            [playerId],
            Now,
            Now,
            ContextDirection.Positive,
            1m,
            Confidence.High,
            null,
            "manual",
            null,
            "Rotation expanded");
        await service.CreateAsync(
            first,
            [PlayerContextImpact.CreateDefault(Guid.NewGuid(), first, playerId)],
            TestContext.Current.CancellationToken);
        await service.VerifyAsync(
            first.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.VerifyAsync(
                first.Id,
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));

        var second = ContextEvent.Create(
            Guid.NewGuid(),
            ContextEventType.RotationChange,
            null,
            playerId,
            [playerId],
            Now,
            Now,
            ContextDirection.Positive,
            1m,
            Confidence.High,
            null,
            "manual",
            null,
            "Rotation contracted");
        await service.CreateAsync(
            second,
            [PlayerContextImpact.CreateDefault(Guid.NewGuid(), second, playerId)],
            TestContext.Current.CancellationToken);
        await service.RejectAsync(
            second.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.VerifyAsync(
                second.Id,
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));
    }

    private sealed class FakeContextEventRepository : IContextEventRepository
    {
        public ContextEvent? Event { get; private set; }

        public PlayerContextImpact? Impact { get; private set; }

        public Guid? ImpactOverrideUserId { get; private set; }

        public Task AddAsync(
            ContextEvent contextEvent,
            IReadOnlyList<PlayerContextImpact> impacts,
            CancellationToken cancellationToken)
        {
            Event = contextEvent;
            Impact = impacts.Single();
            return Task.CompletedTask;
        }

        public Task<ContextEvent?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Event?.Id == id ? Event : null);

        public Task<PlayerContextImpact?> GetImpactAsync(
            Guid contextEventId,
            PlayerId playerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Impact?.ContextEventId == contextEventId
                    && Impact.PlayerId == playerId
                    ? Impact
                    : null);

        public Task<IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>>
            ListForPlayerAsync(
                PlayerId playerId,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<(ContextEvent, PlayerContextImpact)>>(
                Event is not null
                    && Impact is not null
                    && Impact.PlayerId == playerId
                    ? [(Event, Impact)]
                    : []);

        public Task SaveAsync(
            ContextEvent contextEvent,
            CancellationToken cancellationToken)
        {
            Event = contextEvent;
            return Task.CompletedTask;
        }

        public Task SaveImpactOverrideAsync(
            PlayerContextImpact impact,
            Guid userId,
            DateTimeOffset overriddenAt,
            CancellationToken cancellationToken)
        {
            Impact = impact;
            ImpactOverrideUserId = userId;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

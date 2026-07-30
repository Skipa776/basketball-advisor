using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Draft;

public sealed class DraftSessionServiceTests
{
    [Fact]
    public async Task A11_double_submit_is_idempotent_then_conflicts_for_other_player()
    {
        var league = League();
        var repository = new FakeDraftRepository();
        var service = new DraftSessionService(
            repository,
            new FakeLeagueRepository(league));
        var session = await service.CreateAsync(
            league.Id,
            userSlot: 3,
            roundCount: 13,
            TestContext.Current.CancellationToken);
        var firstPlayer = new PlayerId(Guid.NewGuid());
        var secondPlayer = new PlayerId(Guid.NewGuid());

        var first = await service.RecordPickAsync(
            session.Id,
            1,
            firstPlayer,
            TestContext.Current.CancellationToken);
        var repeated = await service.RecordPickAsync(
            session.Id,
            1,
            firstPlayer,
            TestContext.Current.CancellationToken);

        repeated.ShouldBe(first);
        repository.AddedPicks.ShouldBe(1);
        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.RecordPickAsync(
                session.Id,
                1,
                secondPlayer,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A12_only_most_recent_pick_can_be_undone()
    {
        var league = League();
        var service = new DraftSessionService(
            new FakeDraftRepository(),
            new FakeLeagueRepository(league));
        var session = await service.CreateAsync(
            league.Id,
            1,
            13,
            TestContext.Current.CancellationToken);
        await service.RecordPickAsync(
            session.Id,
            1,
            new PlayerId(Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        await service.RecordPickAsync(
            session.Id,
            2,
            new PlayerId(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.UndoPickAsync(
                session.Id,
                1,
                TestContext.Current.CancellationToken));

        var removed = await service.UndoPickAsync(
            session.Id,
            2,
            TestContext.Current.CancellationToken);
        removed.PickNumber.ShouldBe(2);
    }

    private static FantasyLeague League() =>
        new(
            Guid.NewGuid(),
            "Test",
            LeagueType.Points,
            10,
            [new ScoringRule(StatKey.PTS, 1m)],
            [],
            [new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Daily);

    private sealed class FakeLeagueRepository(FantasyLeague league)
        : ILeagueRepository
    {
        public Task AddAsync(
            FantasyLeague value,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<FantasyLeague?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(id == league.Id ? league : null);

        public Task<IReadOnlyList<FantasyLeague>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FantasyLeague>>([league]);

        public Task SaveScoringAsync(
            FantasyLeague value,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeDraftRepository : IDraftRepository
    {
        private DraftSessionRecord? record;

        public int AddedPicks { get; private set; }

        public Task AddSessionAsync(
            DraftSession session,
            Guid leagueId,
            CancellationToken cancellationToken)
        {
            record = new DraftSessionRecord(session, leagueId);
            return Task.CompletedTask;
        }

        public Task<DraftSessionRecord?> GetSessionAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(record?.Session.Id == id ? record : null);

        public Task AddPickAsync(
            DraftPick pick,
            CancellationToken cancellationToken)
        {
            AddedPicks++;
            return Task.CompletedTask;
        }

        public Task RemoveLastPickAsync(
            Guid draftSessionId,
            int pickNumber,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

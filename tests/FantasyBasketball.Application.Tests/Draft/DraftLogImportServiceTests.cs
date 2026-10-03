using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Domain.Draft;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Draft;

public sealed class DraftLogImportServiceTests
{
    [Fact]
    public async Task DL01_snowball_follows_pickers_and_keeps_only_complete_redraft_snake_drafts()
    {
        var source = new FakeSource();
        source.Drafts["seed"] = [Summary("d1"), Summary("auction", type: "auction"), Summary("live", status: "drafting"), Summary("tiny", teams: 4)];
        source.Pickers["d1"] = ["u2", "seed"];
        source.Drafts["u2"] = [Summary("d1"), Summary("d2")];
        var logs = new FakeLogs();

        var result = await new DraftLogImportService(source, logs).ImportAsync("league", 2025, 10, 100, TestContext.Current.CancellationToken);

        logs.Stored.Select(log => log.DraftId).ShouldBe(["d1", "d2"]);
        result.Imported.ShouldBe(2);
        result.Skipped.ShouldBe(3);
        result.UsersVisited.ShouldBe(2);
        source.Fetched.ShouldBe(["d1", "d2"], "rejected and repeated drafts never fetch picks");
    }

    [Fact]
    public async Task DL02_stops_at_the_cap_and_skips_stored_or_incomplete_boards()
    {
        var source = new FakeSource();
        source.Drafts["seed"] = [Summary("stored"), Summary("holes"), Summary("d3"), Summary("d4")];
        source.Short.Add("holes");
        var logs = new FakeLogs { Existing = { "stored" }, Count = 5 };

        var result = await new DraftLogImportService(source, logs).ImportAsync("league", 2025, 6, 100, TestContext.Current.CancellationToken);

        logs.Stored.Select(log => log.DraftId).ShouldBe(["d3"]);
        result.TotalStored.ShouldBe(6);
        result.Skipped.ShouldBe(2);
    }

    private static DraftSummary Summary(string id, string status = "complete", string type = "snake", int teams = 10) =>
        new(id, 2025, status, type, teams, 13, "points", new Dictionary<string, int>(), DateTimeOffset.UnixEpoch);

    private sealed class FakeSource : IDraftLogSource
    {
        public Dictionary<string, List<DraftSummary>> Drafts { get; } = [];

        public Dictionary<string, List<string>> Pickers { get; } = [];

        public HashSet<string> Short { get; } = [];

        public List<string> Fetched { get; } = [];

        public string Name => "fake";

        public Task<IReadOnlyList<string>> SeedUsersAsync(string seed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["seed"]);

        public Task<IReadOnlyList<DraftSummary>> ListUserDraftsAsync(string userId, int season, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DraftSummary>>(Drafts.GetValueOrDefault(userId) ?? []);

        public Task<DraftLog> GetDraftAsync(DraftSummary draft, CancellationToken cancellationToken)
        {
            Fetched.Add(draft.DraftId);
            var pickers = Pickers.GetValueOrDefault(draft.DraftId) ?? [];
            var count = Short.Contains(draft.DraftId) ? 100 : draft.TeamCount * draft.Rounds;
            var picks = Enumerable.Range(1, count)
                .Select(n => new DraftLogPick(n, ((n - 1) / draft.TeamCount) + 1, ((n - 1) % draft.TeamCount) + 1, $"p{n}", $"Player {n}", ["C"],
                    pickers.Count == 0 ? null : pickers[n % pickers.Count]))
                .ToArray();
            return Task.FromResult(new DraftLog(Name, draft.DraftId, draft.Season, draft.TeamCount, draft.Rounds, draft.ScoringType, draft.Slots, draft.StartedAt, picks));
        }
    }

    private sealed class FakeLogs : IDraftLogRepository
    {
        public HashSet<string> Existing { get; } = [];

        public List<DraftLog> Stored { get; } = [];

        public int Count { get; init; }

        public Task<bool> ExistsAsync(string source, string draftId, CancellationToken cancellationToken) =>
            Task.FromResult(Existing.Contains(draftId));

        public Task AddAsync(DraftLog log, CancellationToken cancellationToken)
        {
            Stored.Add(log);
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(string source, int season, CancellationToken cancellationToken) => Task.FromResult(Count);
    }
}

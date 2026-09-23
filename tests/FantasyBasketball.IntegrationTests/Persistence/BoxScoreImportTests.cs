using System.Net;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Trends;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using FantasyBasketball.Infrastructure.Scrapers.BasketballReference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task S30_S33_box_score_import_skips_stored_games_and_isolates_a_failed_page()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = CreateDatabase();
        var aaa = Guid.NewGuid();
        var bbb = Guid.NewGuid();
        var ccc = Guid.NewGuid();
        database.NbaTeams.AddRange(
            NbaTeamRow.Create(aaa, "Alpha", "AAA"),
            NbaTeamRow.Create(bbb, "Beta", "BBB"),
            NbaTeamRow.Create(ccc, "Gamma", "CCC"));
        // 7:30pm ET on Jan 2 is 00:30 UTC on Jan 3; the page date must be Jan 2.
        var tipOff = new DateTimeOffset(2026, 1, 3, 0, 30, 0, TimeSpan.Zero);
        var stored = ScheduleRow(aaa, bbb, tipOff, "Final", "1");
        database.NbaGames.AddRange(
            stored,
            ScheduleRow(ccc, bbb, tipOff, "Final", "2"),
            ScheduleRow(bbb, aaa, tipOff, "3rd Qtr", "3"),
            ScheduleRow(aaa, ccc, tipOff.AddDays(3), "Final", "4"));
        await database.SaveChangesAsync(token);

        var handler = new BoxScorePageHandler();
        BoxScoreImporter Importer(FantasyDbContext context) => new(
            new SingleBbrefClientFactory(handler),
            new GameRepository(context),
            new BoxScoreRepository(context),
            new TeamRepository(context),
            new PlayerIdentityResolver(new PlayerRepository(context), new FixedTimeProvider(tipOff.AddHours(8))),
            new DataImportRunRepository(context),
            new FixedTimeProvider(tipOff.AddHours(8)),
            NullLogger<BoxScoreImporter>.Instance);

        var first = await Importer(database).ImportRegularSeasonAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), null, token);

        handler.Requests.ShouldBe(["/boxscores/202601020AAA.html", "/boxscores/202601020CCC.html"]);
        first.Status.ShouldBe(DataImportRunStatus.Failed);
        first.RowsWritten.ShouldBe(1);
        first.FailureDetail.ShouldNotBeNull().ShouldContain("202601020CCC");
        var snapshot = await database.BoxScoreSnapshots.SingleAsync(token);
        snapshot.GameId.ShouldBe(stored.Id);
        snapshot.PlayedOn.ShouldBe(new DateOnly(2026, 1, 2));
        snapshot.Phase.ShouldBe(NbaGamePhase.RegularSeason.ToString());
        snapshot.Source.ShouldBe(DataSourceName.BasketballReference);

        database.ChangeTracker.Clear();
        await Importer(database).ImportRegularSeasonAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), null, token);

        handler.Requests.Count(path => path.EndsWith("AAA.html", StringComparison.Ordinal)).ShouldBe(1);
        handler.Requests.Count(path => path.EndsWith("CCC.html", StringComparison.Ordinal)).ShouldBe(2);
        (await database.BoxScoreSnapshots.CountAsync(token)).ShouldBe(1);
    }

    private static NbaGameRow ScheduleRow(Guid home, Guid away, DateTimeOffset startsAt, string status, string externalId) =>
        NbaGameRow.Create(Guid.NewGuid(), 2026, startsAt, home, away, 100, 90, status,
            DataSourceName.BallDontLie, externalId, startsAt, null, "balldontlie-v1", 1m, new string('b', 64));

    private sealed class BoxScorePageHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            if (path != "/boxscores/202601020AAA.html")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures/Html/basketball-reference-boxscore-synthetic-2026-09-20.html"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }

    private sealed class SingleBbrefClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://www.basketball-reference.com") };
    }
}

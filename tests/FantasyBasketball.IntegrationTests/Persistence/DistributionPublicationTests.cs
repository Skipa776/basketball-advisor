using System.Text.Json;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task P15_publication_with_the_fitted_models_stores_distributions_the_draft_board_reads()
    {
        var token = TestContext.Current.CancellationToken;
        var player = new PlayerId(Guid.NewGuid());
        var league = LeagueCatalog.CreateEspnDefaultPointsLeague() is var espn
            ? new FantasyLeague(Guid.NewGuid(), "Distribution fixture", LeagueType.Points, 12, espn.ScoringRules, [], espn.RosterSlots, LineupCadence.Daily)
            : throw new InvalidOperationException();
        await using var database = CreateDatabase();
        database.Players.Add(PlayerRow.Create(player.Value, "Center", "center", ["C"], null));
        await database.SaveChangesAsync(token);
        await new LeagueRepository(database).AddAsync(league, token);
        var statistics = new SeasonStatLineRepository(database);
        foreach (var (season, games) in new[] { (2023, 70), (2024, 64), (2025, 72) })
        {
            await statistics.AddAsync(CenterLine(player, season, games), token);
        }

        var models = new ModelVersionRepository(database);
        foreach (var file in new[] { "projection", "minutes", "availability", "covariance" })
        {
            using var golden = JsonDocument.Parse(await File.ReadAllTextAsync(GoldenPath(file), token));
            var name = golden.RootElement.GetProperty("model").GetString()!;
            await models.AddAsync(new ModelVersion(name, $"{name}-golden", DateTimeOffset.UnixEpoch, [2025],
                golden.RootElement.GetProperty("parameters").GetRawText(), "{}", "golden"), token);
            await models.ActivateAsync(name, $"{name}-golden", token);
        }

        await PublicationService(database, DateTimeOffset.UnixEpoch.AddDays(1))
            .RecalculateAsync(league.Id, 2025, DataSourceName.BasketballReference, token);

        var baseline = await database.BaselineProjections.AsNoTracking().SingleAsync(token);
        baseline.ModelVersion.ShouldStartWith("projection-rates-golden+projection-minutes-golden");
        var stored = await database.ProjectionDistributions.AsNoTracking().SingleAsync(token);
        stored.BaselineProjectionId.ShouldBe(baseline.Id);
        stored.SeasonGames.ShouldBe(82);
        var candidate = (await new DraftCandidateRepository(database).ListAsync(league.Id, token)).ShouldHaveSingleItem();
        var distribution = candidate.Distribution.ShouldNotBeNull();
        distribution.PerGameSd.ShouldBeGreaterThan(0m);
        distribution.Games.Mean.ShouldBe((decimal)baseline.ProjectedGamesPlayed, 0.5m);
        distribution.PerGameMean.ShouldBe(candidate.ProjectedSeasonValue / baseline.ProjectedGamesPlayed, 0.01m);
    }

    private static SeasonStatLine CenterLine(PlayerId player, int season, int games)
    {
        var perGame = new Dictionary<StatKey, decimal>
        {
            [StatKey.MIN] = 30m,
            [StatKey.OREB] = 3m,
            [StatKey.DREB] = 8m,
            [StatKey.REB] = 11m,
            [StatKey.AST] = 3m,
            [StatKey.STL] = 1m,
            [StatKey.BLK] = 2m,
            [StatKey.TOV] = 2m,
            [StatKey.FGM] = 7m,
            [StatKey.FGA] = 12m,
            [StatKey.FG3M] = 0m,
            [StatKey.FG3A] = 0m,
            [StatKey.FTM] = 3m,
            [StatKey.FTA] = 4m,
            [StatKey.PF] = 3m,
            [StatKey.PTS] = 17m,
        };
        return new SeasonStatLine(player, season, games, 30m, new StatLine(perGame),
            new StatLine(perGame.ToDictionary(pair => pair.Key, pair => pair.Value * games)), null,
            new DataProvenance(DataSourceName.BasketballReference, $"center{season}", DateTimeOffset.UnixEpoch, null,
                "basketball-reference-v1", 1m, new string('c', 64)),
            25 + (season - 2023));
    }

    private static string GoldenPath(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FantasyBasketball.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."),
            "tools", "modeling", "goldens", $"{name}.json");
    }
}

using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed class PersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17")
        .Build();

    private DbContextOptions<FantasyDbContext> options = null!;

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        options = new DbContextOptionsBuilder<FantasyDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var database = new FantasyDbContext(options);
        await database.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task Migration_from_empty_and_core_round_trip_succeed()
    {
        var playerId = new PlayerId(Guid.NewGuid());
        var player = new Player(
            playerId,
            "Nikola Jokic",
            "nikola jokic",
            null,
            ["C"],
            new DateOnly(1995, 2, 19));
        var league = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
        var provenance = new DataProvenance(
            "manual",
            null,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            null,
            "manual-v1",
            1m,
            new string('a', 64));
        var season = new SeasonStatLine(
            playerId,
            2026,
            82,
            34.5m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 25m,
                [StatKey.REB] = 12m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 2050m,
                [StatKey.REB] = 984m,
            }),
            0.29m,
            provenance);

        await using (var database = new FantasyDbContext(options))
        {
            await new PlayerRepository(database).AddAsync(player, CancellationToken.None);
            await new LeagueRepository(database).AddAsync(league, CancellationToken.None);
            await new SeasonStatLineRepository(database)
                .AddAsync(season, CancellationToken.None);
        }

        await using (var database = new FantasyDbContext(options))
        {
            var storedPlayer = await new PlayerRepository(database)
                .GetAsync(playerId, CancellationToken.None);
            var storedLeague = await new LeagueRepository(database)
                .GetAsync(league.Id, CancellationToken.None);
            var storedSeason = await new SeasonStatLineRepository(database)
                .GetAsync(playerId, 2026, "manual", CancellationToken.None);

            storedPlayer.ShouldNotBeNull();
            storedPlayer.Id.ShouldBe(player.Id);
            storedPlayer.FullName.ShouldBe(player.FullName);
            storedPlayer.NormalizedName.ShouldBe(player.NormalizedName);
            storedPlayer.Positions.ShouldBe(player.Positions);
            storedPlayer.BirthDate.ShouldBe(player.BirthDate);
            storedLeague.ShouldNotBeNull();
            storedLeague.Name.ShouldBe(league.Name);
            storedLeague.ScoringRules.ShouldBe(league.ScoringRules);
            storedSeason.ShouldNotBeNull();
            storedSeason.PerGame[StatKey.PTS].ShouldBe(25m);
            storedSeason.Provenance.ShouldBe(provenance);
        }
    }

    [Fact]
    public async Task Duplicate_provider_identity_fails_at_the_database()
    {
        var first = new PlayerId(Guid.NewGuid());
        var second = new PlayerId(Guid.NewGuid());

        await using var database = new FantasyDbContext(options);
        database.Players.AddRange(
            PlayerRow.Create(first.Value, "First", "first", [], null),
            PlayerRow.Create(second.Value, "Second", "second", [], null));
        database.ExternalPlayerIdentities.AddRange(
            ExternalPlayerIdentityRow.Create(first.Value, "balldontlie", "42"),
            ExternalPlayerIdentityRow.Create(second.Value, "balldontlie", "42"));

        await Should.ThrowAsync<DbUpdateException>(
            database.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Modified_baseline_is_rejected()
    {
        var player = PlayerRow.Create(Guid.NewGuid(), "Player", "player", ["PG"], null);
        var baseline = BaselineProjectionRow.Create(
            Guid.NewGuid(),
            player.Id,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));

        await using (var database = new FantasyDbContext(options))
        {
            database.Players.Add(player);
            database.BaselineProjections.Add(baseline);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var database = new FantasyDbContext(options))
        {
            var stored = await database.BaselineProjections.SingleAsync(
                value => value.Id == baseline.Id,
                TestContext.Current.CancellationToken);
            stored.ModelVersion = "mutated";

            await Should.ThrowAsync<InvalidOperationException>(
                async () => await database.SaveChangesAsync(
                    TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void Repository_interfaces_are_async_domain_only_and_append_only()
    {
        var interfaces = typeof(IPlayerRepository).Assembly
            .GetTypes()
            .Where(type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToArray();

        interfaces.ShouldNotBeEmpty();
        interfaces.SelectMany(type => type.GetMethods())
            .ShouldNotContain(method => method.Name.Contains("Update", StringComparison.Ordinal));
        interfaces.SelectMany(type => type.GetMethods())
            .ShouldNotContain(method => method.ReturnType.IsGenericType
                && method.ReturnType.GetGenericTypeDefinition() == typeof(IQueryable<>));
    }

    [Fact]
    public void Domain_and_application_dependency_boundaries_are_clean()
    {
        var domainReferences = typeof(Player).Assembly.GetReferencedAssemblies()
            .Select(value => value.Name)
            .ToArray();
        domainReferences.Any(value =>
            value != null
            && (value.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || value.StartsWith("Npgsql", StringComparison.Ordinal)
                || value == "System.Net.Http")).ShouldBeFalse();

        var applicationReferences = typeof(IPlayerRepository).Assembly
            .GetReferencedAssemblies()
            .Select(value => value.Name)
            .ToArray();
        applicationReferences.Any(value =>
            value != null
            && value.StartsWith(
                "FantasyBasketball.Infrastructure",
                StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Enums_are_stored_as_names()
    {
        var league = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());

        await using var database = new FantasyDbContext(options);
        await new LeagueRepository(database).AddAsync(
            league,
            TestContext.Current.CancellationToken);

        await database.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT type FROM fantasy_league WHERE id = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = league.Id;
        command.Parameters.Add(parameter);

        var stored = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        stored.ShouldBe(nameof(LeagueType.Points));
    }

    [Fact]
    public async Task League_deletion_cascades_to_drafts_but_player_history_restricts_delete()
    {
        var player = PlayerRow.Create(Guid.NewGuid(), "Player", "player", ["PG"], null);
        var league = FantasyLeagueRow.Create(
            Guid.NewGuid(),
            "League",
            nameof(LeagueType.Points),
            10,
            [],
            nameof(LineupCadence.Daily));
        var session = DraftSessionRow.Create(Guid.NewGuid(), league.Id, 13);
        var pick = DraftPickRow.Create(Guid.NewGuid(), session.Id, player.Id, 1);
        var season = SeasonStatLineRow.Create(
            player.Id,
            2026,
            1,
            1m,
            "{}",
            "{}",
            null,
            "manual",
            null,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            null,
            "manual-v1",
            1m,
            new string('b', 64));

        await using (var database = new FantasyDbContext(options))
        {
            database.AddRange(player, league, session, pick, season);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            database.FantasyLeagues.Remove(league);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            (await database.DraftSessions.CountAsync(TestContext.Current.CancellationToken))
                .ShouldBe(0);
            (await database.DraftPicks.CountAsync(TestContext.Current.CancellationToken))
                .ShouldBe(0);
        }

        await using (var database = new FantasyDbContext(options))
        {
            var storedPlayer = await database.Players.SingleAsync(
                value => value.Id == player.Id,
                TestContext.Current.CancellationToken);
            database.Players.Remove(storedPlayer);

            await Should.ThrowAsync<DbUpdateException>(
                async () => await database.SaveChangesAsync(
                    TestContext.Current.CancellationToken));
        }
    }
}

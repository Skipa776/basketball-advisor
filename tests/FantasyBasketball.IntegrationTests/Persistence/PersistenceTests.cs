using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using FantasyBasketball.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;
using CanonicalAdpEntry = FantasyBasketball.Domain.Draft.AdpEntry;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17")
        .Build();

    private DbContextOptions<FantasyDbContext> options = null!;
    private readonly Guid ownerId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        options = new DbContextOptionsBuilder<FantasyDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var database = CreateDatabase();
        await database.Database.MigrateAsync();
        database.Users.Add(new FantasyUser
        {
            Id = ownerId,
            UserName = "persistence-test",
            NormalizedUserName = "PERSISTENCE-TEST",
            DisplayName = "Persistence Test",
            CreatedAt = DateTimeOffset.UnixEpoch,
        });
        await database.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    private FantasyDbContext CreateDatabase() =>
        new(options, new FixedUserContext(ownerId));

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
            DataSourceName.Manual,
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

        await using (var database = CreateDatabase())
        {
            await new PlayerRepository(database).AddAsync(player, CancellationToken.None);
            await new LeagueRepository(database).AddAsync(league, CancellationToken.None);
            await new SeasonStatLineRepository(database)
                .AddAsync(season, CancellationToken.None);
        }

        await using (var database = CreateDatabase())
        {
            var storedPlayer = await new PlayerRepository(database)
                .GetAsync(playerId, CancellationToken.None);
            var storedLeague = await new LeagueRepository(database)
                .GetAsync(league.Id, CancellationToken.None);
            var storedSeason = await new SeasonStatLineRepository(database)
                .GetAsync(
                    playerId,
                    2026,
                    DataSourceName.Manual,
                    CancellationToken.None);

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

        await using var database = CreateDatabase();
        database.Players.AddRange(
            PlayerRow.Create(first.Value, "First", "first", [], null),
            PlayerRow.Create(second.Value, "Second", "second", [], null));
        database.ExternalPlayerIdentities.AddRange(
            ExternalPlayerIdentityRow.Create(
                first.Value,
                DataSourceName.BallDontLie,
                "42"),
            ExternalPlayerIdentityRow.Create(
                second.Value,
                DataSourceName.BallDontLie,
                "42"));

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
            0m,
            "{}",
            "{}",
            0,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            "persistence-test-v1");

        await using (var database = CreateDatabase())
        {
            database.Players.Add(player);
            database.BaselineProjections.Add(baseline);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var database = CreateDatabase())
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

        await using var database = CreateDatabase();
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
        var session = DraftSessionRow.Create(
            Guid.NewGuid(),
            league.Id,
            13,
            league.TeamCount,
            1);
        var pick = DraftPickRow.Create(Guid.NewGuid(), session.Id, player.Id, 1);
        var season = SeasonStatLineRow.Create(
            player.Id,
            2026,
            1,
            1m,
            "{}",
            "{}",
            null,
            DataSourceName.Manual,
            null,
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            null,
            "manual-v1",
            1m,
            new string('b', 64));

        await using (var database = CreateDatabase())
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

        await using (var database = CreateDatabase())
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

    [Fact]
    public async Task Ambiguous_identity_persists_pending_match_without_a_link()
    {
        var first = new Player(
            new PlayerId(Guid.NewGuid()),
            "Marcus Williams",
            PlayerName.Normalize("Marcus Williams"),
            null,
            ["G"],
            null);
        var second = new Player(
            new PlayerId(Guid.NewGuid()),
            first.FullName,
            first.NormalizedName,
            null,
            first.Positions,
            null);
        var provenance = new DataProvenance(
            DataSourceName.BallDontLie,
            "99",
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            null,
            "balldontlie-v1",
            DataSourceConfidence.OfficialApi,
            new string('c', 64));

        await using var database = CreateDatabase();
        var repository = new PlayerRepository(database);
        await repository.AddAsync(first, TestContext.Current.CancellationToken);
        await repository.AddAsync(second, TestContext.Current.CancellationToken);
        var resolver = new PlayerIdentityResolver(
            repository,
            new FixedTimeProvider(provenance.FetchedAt));
        var runRepository = new DataImportRunRepository(database);
        var service = new ImportPlayersService(
            resolver,
            repository,
            runRepository,
            new EfImportTransaction(database),
            new FixedTimeProvider(provenance.FetchedAt));

        var run = await service.ImportAsync(
            DataSourceName.BallDontLie,
            [new ExternalPlayer("99", "Marcus Williams", null, ["G"], null, provenance)],
            TestContext.Current.CancellationToken);
        var storedRun = await runRepository.GetAsync(
            run.Id,
            TestContext.Current.CancellationToken);

        run.PendingIdentityMatches.ShouldBe(1);
        run.RowsWritten.ShouldBe(0);
        storedRun.ShouldBe(run);
        (await database.PendingIdentityMatches.CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(1);
        (await database.ExternalPlayerIdentities.CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Failed_import_rolls_back_real_database_writes_and_records_failed_run()
    {
        await using var database = CreateDatabase();
        var repository = new InterruptingPlayerRepository(
            new PlayerRepository(database),
            beforeResolvedWrite: attempt =>
            {
                if (attempt == 2)
                {
                    throw new InvalidOperationException("Simulated write failure.");
                }
            });
        var runs = new DataImportRunRepository(database);
        var service = new ImportPlayersService(
            new PlayerIdentityResolver(repository, new FixedTimeProvider(DateTimeOffset.UnixEpoch)),
            repository,
            runs,
            new EfImportTransaction(database),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var run = await service.ImportAsync(
            DataSourceName.BallDontLie,
            [
                CreateExternalPlayer("first", "First Player"),
                CreateExternalPlayer("second", "Second Player"),
            ],
            TestContext.Current.CancellationToken);

        run.Status.ShouldBe(DataImportRunStatus.Failed);
        (await database.Players.AsNoTracking().CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(0);
        (await database.ExternalPlayerIdentities.AsNoTracking().CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(0);
        (await database.DataImportRuns.AsNoTracking().CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Canceled_import_rolls_back_real_database_writes()
    {
        using var cancellation = new CancellationTokenSource();
        await using var database = CreateDatabase();
        var repository = new InterruptingPlayerRepository(
            new PlayerRepository(database),
            afterResolvedWrite: _ => cancellation.Cancel());
        var service = new ImportPlayersService(
            new PlayerIdentityResolver(repository, new FixedTimeProvider(DateTimeOffset.UnixEpoch)),
            repository,
            new DataImportRunRepository(database),
            new EfImportTransaction(database),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        await Should.ThrowAsync<OperationCanceledException>(() => service.ImportAsync(
            DataSourceName.BallDontLie,
            [
                CreateExternalPlayer("first", "First Player"),
                CreateExternalPlayer("second", "Second Player"),
            ],
            cancellation.Token));

        (await database.Players.AsNoTracking().CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(0);
        (await database.ExternalPlayerIdentities.AsNoTracking().CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(0);
        (await database.DataImportRuns.AsNoTracking().CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Imported_team_and_game_round_trip_with_non_null_provenance()
    {
        var home = new NbaTeam(new NbaTeamId(Guid.NewGuid()), "Atlanta Hawks", "ATL");
        var away = new NbaTeam(new NbaTeamId(Guid.NewGuid()), "Boston Celtics", "BOS");
        var homeProvenance = CreateProvenance("1", 'e');
        var awayProvenance = CreateProvenance("2", 'f');
        var game = new NbaGame(
            Guid.NewGuid(),
            2026,
            new DateTimeOffset(2026, 1, 15, 0, 30, 0, TimeSpan.Zero),
            home.Id,
            away.Id,
            112,
            108,
            "Final",
            CreateProvenance("9001", 'a'));

        await using var database = CreateDatabase();
        var teamRepository = new TeamRepository(database);
        await teamRepository.AddAsync(
            home,
            homeProvenance,
            TestContext.Current.CancellationToken);
        await teamRepository.AddAsync(
            away,
            awayProvenance,
            TestContext.Current.CancellationToken);
        var gameRepository = new GameRepository(database);
        await gameRepository.AddAsync(game, TestContext.Current.CancellationToken);

        var storedHome = await teamRepository.FindByAbbreviationAsync(
            "ATL",
            TestContext.Current.CancellationToken);
        var storedGame = await gameRepository.GetBySourceAsync(
            DataSourceName.BallDontLie,
            "9001",
            TestContext.Current.CancellationToken);

        storedHome.ShouldBe(home);
        storedGame.ShouldBe(game);
        var sourceRow = await database.NbaTeamSources.AsNoTracking().SingleAsync(
            source => source.ExternalId == "1",
            TestContext.Current.CancellationToken);
        sourceRow.Source.ShouldBe(DataSourceName.BallDontLie);
        sourceRow.ParserVersion.ShouldBe("balldontlie-v1");
        sourceRow.RawRecordHash.ShouldBe(homeProvenance.RawRecordHash);
    }

    [Fact]
    public async Task Adp_entry_round_trips_with_non_null_provenance()
    {
        var player = new Player(
            new PlayerId(Guid.NewGuid()),
            "Nikola Jokic",
            PlayerName.Normalize("Nikola Jokic"),
            null,
            ["C"],
            null);
        var provenance = new DataProvenance(
            DataSourceName.Manual,
            "jokic",
            DateTimeOffset.UnixEpoch,
            null,
            "manual-v1",
            DataSourceConfidence.ManualEntry,
            new string('b', 64));
        var entry = new CanonicalAdpEntry(
            Guid.NewGuid(),
            player.Id,
            2.3m,
            1.1m,
            provenance);

        await using (var database = CreateDatabase())
        {
            await new PlayerRepository(database).AddAsync(
                player,
                TestContext.Current.CancellationToken);
            await new AdpRepository(database).AddAsync(
                entry,
                TestContext.Current.CancellationToken);
        }

        await using (var database = CreateDatabase())
        {
            var stored = await new AdpRepository(database).GetLatestAsync(
                player.Id,
                TestContext.Current.CancellationToken);

            stored.ShouldBe(entry);
            var row = await database.AdpEntries.AsNoTracking().SingleAsync(
                candidate => candidate.Id == entry.Id,
                TestContext.Current.CancellationToken);
            row.Source.ShouldBe(DataSourceName.Manual);
            row.ExternalId.ShouldBe("jokic");
            row.ParserVersion.ShouldBe("manual-v1");
            row.RawRecordHash.ShouldBe(provenance.RawRecordHash);
        }
    }

    [Fact]
    public async Task P02_observed_and_baseline_round_trip_separately_at_four_decimals()
    {
        var player = new Player(
            new PlayerId(Guid.NewGuid()),
            "Projection Player",
            "projection player",
            null,
            ["G"],
            null);
        var source = new SeasonStatLine(
            player.Id,
            2026,
            40,
            30m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 10m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 1200m,
                [StatKey.PTS] = 400m,
            }),
            null,
            new DataProvenance(
                DataSourceName.Manual,
                "projection-player",
                DateTimeOffset.UnixEpoch,
                null,
                "manual-v1",
                DataSourceConfidence.ManualEntry,
                new string('c', 64)));
        var observed = new ObservedStats(
            player.Id,
            source,
            DateTimeOffset.UnixEpoch);
        var baseline = new BaselineProjection(
            Guid.NewGuid(),
            player.Id,
            30.12345m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 0.12345m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 30.12345m,
                [StatKey.PTS] = 3.718271525m,
            }),
            60,
            DateTimeOffset.UnixEpoch,
            "baseline-v1");

        await using (var database = CreateDatabase())
        {
            await new PlayerRepository(database).AddAsync(
                player,
                TestContext.Current.CancellationToken);
            await new SeasonStatLineRepository(database).AddAsync(
                source,
                TestContext.Current.CancellationToken);
            await new ProjectionRepository(database).AddAsync(
                observed,
                baseline,
                TestContext.Current.CancellationToken);
        }

        await using (var database = CreateDatabase())
        {
            var repository = new ProjectionRepository(database);
            var storedObserved = await repository.GetLatestObservedAsync(
                player.Id,
                TestContext.Current.CancellationToken);
            var storedBaseline = await repository.GetBaselineAsync(
                baseline.Id,
                TestContext.Current.CancellationToken);

            storedObserved.ShouldNotBeNull();
            storedObserved.Source.SeasonEndYear.ShouldBe(2026);
            storedObserved.Source.Totals[StatKey.PTS].ShouldBe(400m);
            storedBaseline.ShouldNotBeNull();
            storedBaseline.ProjectedMinutesPerGame.ShouldBe(30.1235m);
            storedBaseline.PerMinuteRates[StatKey.PTS].ShouldBe(0.1235m);
            storedBaseline.ProjectedPerGame[StatKey.PTS].ShouldBe(3.7183m);
            storedBaseline.ComputedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        }
    }

    [Fact]
    public async Task Context_adjusted_projection_and_value_round_trip()
    {
        var playerId = new PlayerId(Guid.NewGuid());
        var player = new Player(
            playerId,
            "Context Player",
            "context player",
            null,
            ["G"],
            null);
        var league = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
        var baseline = new BaselineProjection(
            Guid.NewGuid(),
            playerId,
            30m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 1m,
                [StatKey.PTS] = 0.5m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 30m,
                [StatKey.PTS] = 15m,
            }),
            70,
            DateTimeOffset.UnixEpoch,
            "context-persistence-v1");
        var contextEvent = ContextEvent.Create(
            Guid.NewGuid(),
            ContextEventType.RotationChange,
            null,
            playerId,
            [playerId],
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            ContextDirection.Positive,
            1m,
            Confidence.High,
            null,
            DataSourceName.Manual,
            null,
            "Rotation expanded");
        var impact = PlayerContextImpact.CreateDefault(
            Guid.NewGuid(),
            contextEvent,
            playerId);
        var adjusted = new ContextApplier(new ConfidenceCalculator()).Apply(
            Guid.NewGuid(),
            baseline,
            [contextEvent],
            [impact],
            DateTimeOffset.UnixEpoch);

        await using (var database = CreateDatabase())
        {
            await new PlayerRepository(database).AddAsync(
                player,
                TestContext.Current.CancellationToken);
            await new LeagueRepository(database).AddAsync(
                league,
                TestContext.Current.CancellationToken);
            database.BaselineProjections.Add(BaselineProjectionRow.Create(
                baseline.Id,
                playerId.Value,
                baseline.ProjectedMinutesPerGame,
                """{"MIN":1,"PTS":0.5}""",
                """{"MIN":30,"PTS":15}""",
                baseline.ProjectedGamesPlayed,
                baseline.ComputedAt,
                baseline.ModelVersion));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            var contextRepository = new ContextEventRepository(database);
            await contextRepository.AddAsync(
                contextEvent,
                [impact],
                TestContext.Current.CancellationToken);
            contextEvent.VerifyByHuman(Guid.NewGuid(), DateTimeOffset.UnixEpoch);
            await contextRepository.SaveAsync(
                contextEvent,
                TestContext.Current.CancellationToken);
            await contextRepository.SaveImpactOverrideAsync(
                impact.Override(2m, 0m, 0m, 0m, 0m, 0.1m, -0.1m),
                Guid.NewGuid(),
                DateTimeOffset.UnixEpoch,
                TestContext.Current.CancellationToken);

            var projectionRepository = new ProjectionRepository(database);
            await projectionRepository.AddAdjustedAsync(
                adjusted,
                TestContext.Current.CancellationToken);
            await projectionRepository.AddFantasyValueAsync(
                new FantasyValue(
                    playerId,
                    league.Id,
                    25.12345m,
                    1758.64155m,
                    adjusted.Id),
                league,
                DateTimeOffset.UnixEpoch,
                null,
                TestContext.Current.CancellationToken);
        }

        await using (var database = CreateDatabase())
        {
            var contextRepository = new ContextEventRepository(database);
            var storedEvent = await contextRepository.GetAsync(
                contextEvent.Id,
                TestContext.Current.CancellationToken);
            var storedImpact = await contextRepository.GetImpactAsync(
                contextEvent.Id,
                playerId,
                TestContext.Current.CancellationToken);
            var projectionRepository = new ProjectionRepository(database);
            var storedAdjusted = await projectionRepository.GetAdjustedAsync(
                adjusted.Id,
                TestContext.Current.CancellationToken);
            var storedValue = await projectionRepository.GetFantasyValueAsync(
                playerId,
                league.Id,
                TestContext.Current.CancellationToken);

            storedEvent.ShouldNotBeNull();
            storedEvent.Verification.ShouldBe(VerificationState.Verified);
            storedImpact.ShouldNotBeNull();
            storedImpact.IsOverridden.ShouldBeTrue();
            storedImpact.MinutesDelta.ShouldBe(2m);
            storedAdjusted.ShouldNotBeNull();
            storedAdjusted.BaselineProjectionId.ShouldBe(baseline.Id);
            storedAdjusted.AppliedContextEventIds.ShouldBe([contextEvent.Id]);
            storedValue.ShouldNotBeNull();
            storedValue.PerGame.ShouldBe(25.1235m);
            storedValue.SeasonTotal.ShouldBe(1758.6416m);
        }
    }

    [Fact]
    public async Task Draft_pick_idempotency_and_last_pick_undo_cross_postgresql()
    {
        var league = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
        var first = new Player(
            new PlayerId(Guid.NewGuid()),
            "First Pick",
            "first pick",
            null,
            ["G"],
            null);
        var second = new Player(
            new PlayerId(Guid.NewGuid()),
            "Second Pick",
            "second pick",
            null,
            ["F"],
            null);

        await using var database = CreateDatabase();
        var leagues = new LeagueRepository(database);
        await leagues.AddAsync(league, TestContext.Current.CancellationToken);
        var players = new PlayerRepository(database);
        await players.AddAsync(first, TestContext.Current.CancellationToken);
        await players.AddAsync(second, TestContext.Current.CancellationToken);
        var service = new DraftSessionService(
            new DraftRepository(database),
            leagues);
        var session = await service.CreateAsync(
            league.Id,
            1,
            13,
            TestContext.Current.CancellationToken);

        var pick = await service.RecordPickAsync(
            session.Id,
            1,
            first.Id,
            TestContext.Current.CancellationToken);
        var repeated = await service.RecordPickAsync(
            session.Id,
            1,
            first.Id,
            TestContext.Current.CancellationToken);

        repeated.ShouldBe(pick);
        (await database.DraftPicks.CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(1);
        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.RecordPickAsync(
                session.Id,
                1,
                second.Id,
                TestContext.Current.CancellationToken));
        var removed = await service.UndoPickAsync(
            session.Id,
            1,
            TestContext.Current.CancellationToken);
        removed.ShouldBe(pick);
        (await database.DraftPicks.CountAsync(
            TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Recommendation_and_evidence_round_trip()
    {
        var player = new Player(
            new PlayerId(Guid.NewGuid()),
            "Recommended Player",
            "recommended player",
            null,
            ["G"],
            null);
        var recommendation = new Recommendation(
            Guid.NewGuid(),
            "Draft now",
            player.Id,
            12.34567m,
            Confidence.Moderate,
            [
                new RecommendationEvidence(
                    EvidenceKind.Opportunity,
                    EvidencePolarity.Supporting,
                    "Above replacement",
                    4.56789m),
                new RecommendationEvidence(
                    EvidenceKind.DataQuality,
                    EvidencePolarity.Risk,
                    "Source is stale",
                    null),
            ]);

        await using (var database = CreateDatabase())
        {
            await new PlayerRepository(database).AddAsync(
                player,
                TestContext.Current.CancellationToken);
            await new RecommendationRepository(database).AddRangeAsync(
                [recommendation],
                TestContext.Current.CancellationToken);
        }

        await using (var database = CreateDatabase())
        {
            var stored = await new RecommendationRepository(database).GetAsync(
                recommendation.Id,
                TestContext.Current.CancellationToken);

            stored.ShouldNotBeNull();
            stored.Score.ShouldBe(12.3457m);
            stored.Confidence.ShouldBe(Confidence.Moderate);
            stored.Evidence.Count.ShouldBe(2);
            stored.Evidence[0].Magnitude.ShouldBe(4.5679m);
            stored.Evidence[1].Kind.ShouldBe(EvidenceKind.DataQuality);
        }
    }

    private static DataProvenance CreateProvenance(string externalId, char hashCharacter) =>
        new(
            DataSourceName.BallDontLie,
            externalId,
            DateTimeOffset.UnixEpoch,
            null,
            "balldontlie-v1",
            DataSourceConfidence.OfficialApi,
            new string(hashCharacter, 64));

    private static ExternalPlayer CreateExternalPlayer(string externalId, string fullName) =>
        new(
            externalId,
            fullName,
            null,
            ["G"],
            null,
            new DataProvenance(
                DataSourceName.BallDontLie,
                externalId,
                DateTimeOffset.UnixEpoch,
                null,
                "balldontlie-v1",
                DataSourceConfidence.OfficialApi,
                new string('d', 64)));

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FixedUserContext(Guid userId) : IUserContext
    {
        public Guid CurrentUserId => userId;
    }

    private sealed class InterruptingPlayerRepository(
        IPlayerRepository inner,
        Action<int>? beforeResolvedWrite = null,
        Action<int>? afterResolvedWrite = null) : IPlayerRepository
    {
        private int resolvedWriteAttempts;
        public Task SavePositionsAsync(PlayerId id, IReadOnlyList<string> positions, CancellationToken cancellationToken) =>
            inner.SavePositionsAsync(id, positions, cancellationToken);

        public Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken) =>
            inner.SaveCurrentTeamAsync(id, teamId, cancellationToken);


        public Task AddAsync(Player player, CancellationToken cancellationToken) =>
            inner.AddAsync(player, cancellationToken);

        public Task<Player?> GetAsync(
            PlayerId id,
            CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public Task<Player?> FindByExternalIdentityAsync(
            string provider,
            string externalId,
            CancellationToken cancellationToken) =>
            inner.FindByExternalIdentityAsync(provider, externalId, cancellationToken);

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) =>
            inner.FindByNormalizedNameAsync(normalizedName, cancellationToken);

        public Task<ExternalPlayerIdentity?> FindIdentityAsync(
            PlayerId playerId,
            string provider,
            CancellationToken cancellationToken) =>
            inner.FindIdentityAsync(playerId, provider, cancellationToken);

        public async Task AddResolvedIdentityAsync(
            Player player,
            ExternalPlayerIdentity identity,
            bool addPlayer,
            CancellationToken cancellationToken)
        {
            resolvedWriteAttempts++;
            beforeResolvedWrite?.Invoke(resolvedWriteAttempts);
            await inner.AddResolvedIdentityAsync(
                player,
                identity,
                addPlayer,
                cancellationToken);
            afterResolvedWrite?.Invoke(resolvedWriteAttempts);
        }

        public Task AddPendingIdentityMatchAsync(
            PendingIdentityMatch pendingMatch,
            CancellationToken cancellationToken) =>
            inner.AddPendingIdentityMatchAsync(pendingMatch, cancellationToken);
    }
}

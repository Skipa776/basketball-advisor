using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Auth;

public sealed class OwnershipClaimTests : IAsyncLifetime
{
    private const string NullableOwnershipMigration =
        "20260730051545_IdentityAndNullableOwnership";
    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:17").Build();

    public async ValueTask InitializeAsync() => await postgres.StartAsync();

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task U06_U07_U16_first_owner_claims_rows_before_non_null_migration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<AuthOptions>();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<FantasyDbContext>(options =>
            options.UseNpgsql(postgres.GetConnectionString()));
        services.AddIdentityCore<FantasyUser>(ConfigureIdentity)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FantasyDbContext>();
        services.AddScoped<RegistrationService>();
        await using var provider = services.BuildServiceProvider();

        var leagueId = Guid.NewGuid();
        var scoringRuleId = Guid.NewGuid();
        var rosterSlotId = Guid.NewGuid();
        await using (var setupScope = provider.CreateAsyncScope())
        {
            var database = setupScope.ServiceProvider
                .GetRequiredService<FantasyDbContext>();
            await database.GetService<IMigrator>().MigrateAsync(
                NullableOwnershipMigration,
                cancellationToken);
            await InsertPreAuthLeagueAsync(
                database,
                leagueId,
                scoringRuleId,
                rosterSlotId,
                cancellationToken);
        }

        RegistrationOutcome first;
        await using (var registrationScope = provider.CreateAsyncScope())
        {
            first = await registrationScope.ServiceProvider
                .GetRequiredService<RegistrationService>()
                .RegisterAsync(
                    "owner@example.test",
                    "correct horse battery staple",
                    "Instance Owner",
                    cancellationToken);
        }

        first.Succeeded.ShouldBeTrue();
        first.User.ShouldNotBeNull();
        first.User.IsInstanceOwner.ShouldBeTrue();

        await using (var migrationScope = provider.CreateAsyncScope())
        {
            var database = migrationScope.ServiceProvider
                .GetRequiredService<FantasyDbContext>();
            (await CountClaimedRowsAsync(
                database,
                first.User.Id,
                cancellationToken)).ShouldBe(3);
            await database.GetService<IMigrator>().MigrateAsync(
                cancellationToken: cancellationToken);
            (await CountOwnedColumnsAsync(
                database,
                "NO",
                cancellationToken)).ShouldBe(10);
            (await CountOwnedColumnsAsync(
                database,
                "YES",
                cancellationToken)).ShouldBe(1);
            (await ScalarAsync<long>(
                database,
                "SELECT COUNT(*) FROM fantasy_league WHERE id = @id",
                leagueId,
                cancellationToken)).ShouldBe(1);
        }

        await using var secondScope = provider.CreateAsyncScope();
        var second = await secondScope.ServiceProvider
            .GetRequiredService<RegistrationService>()
            .RegisterAsync(
                "second@example.test",
                "another correct horse battery",
                "Second User",
                cancellationToken);
        second.RegistrationClosed.ShouldBeTrue();
        second.User.ShouldBeNull();
        var databaseAfter = secondScope.ServiceProvider
            .GetRequiredService<FantasyDbContext>();
        (await databaseAfter.Users.CountAsync(cancellationToken)).ShouldBe(1);
        (await databaseAfter.UserRoles.CountAsync(cancellationToken)).ShouldBe(1);
    }

    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    }

    private static async Task InsertPreAuthLeagueAsync(
        FantasyDbContext database,
        Guid leagueId,
        Guid scoringRuleId,
        Guid rosterSlotId,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO fantasy_league
                (id, name, type, team_count, categories, cadence, owner_id)
            VALUES
                (@league_id, 'Pre-auth league', 'Points', 10, '{}', 'Daily', NULL);
            INSERT INTO scoring_rule
                (id, fantasy_league_id, stat, points_per_unit, ordinal, owner_id)
            VALUES
                (@rule_id, @league_id, 'PTS', 1, 0, NULL);
            INSERT INTO roster_slot
                (id, fantasy_league_id, kind, ordinal, owner_id)
            VALUES
                (@slot_id, @league_id, 'UTIL', 0, NULL);
            """;
        AddParameter(command, "league_id", leagueId);
        AddParameter(command, "rule_id", scoringRuleId);
        AddParameter(command, "slot_id", rosterSlotId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> CountClaimedRowsAsync(
        FantasyDbContext database,
        Guid ownerId,
        CancellationToken cancellationToken) =>
        await ScalarAsync<long>(
            database,
            """
            SELECT
                (SELECT COUNT(*) FROM fantasy_league WHERE owner_id = @id)
              + (SELECT COUNT(*) FROM scoring_rule WHERE owner_id = @id)
              + (SELECT COUNT(*) FROM roster_slot WHERE owner_id = @id)
            """,
            ownerId,
            cancellationToken);

    private static async Task<long> CountOwnedColumnsAsync(
        FantasyDbContext database,
        string nullable,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND column_name = 'owner_id'
              AND is_nullable = @nullable
            """;
        AddParameter(command, "nullable", nullable);
        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Query returned no value."));
    }

    private static async Task<T> ScalarAsync<T>(
        FantasyDbContext database,
        string commandText,
        object value,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameter(command, "id", value);
        return (T)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Query returned no value."));
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

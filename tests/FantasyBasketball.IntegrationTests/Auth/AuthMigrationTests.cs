using FantasyBasketball.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Auth;

public sealed class AuthMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:17").Build();

    public async ValueTask InitializeAsync() => await postgres.StartAsync();

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task U17_identity_and_nullable_ownership_share_one_migration_history()
    {
        var options = new DbContextOptionsBuilder<FantasyDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var database = new FantasyDbContext(options);

        await database.Database.MigrateAsync(
            TestContext.Current.CancellationToken);

        (await ScalarAsync<long>(
            database,
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('fantasy_user', '__EFMigrationsHistory')
            """)).ShouldBe(2);
        (await ScalarAsync<string>(
            database,
            """
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'fantasy_league'
              AND column_name = 'owner_id'
            """)).ShouldBe("YES");

        typeof(FantasyDbContext).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && typeof(DbContext).IsAssignableFrom(type))
            .ShouldBe([typeof(FantasyDbContext)]);
    }

    private static async Task<T> ScalarAsync<T>(
        FantasyDbContext database,
        string commandText)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (T)(await command.ExecuteScalarAsync(
            TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("Query returned no value."));
    }
}

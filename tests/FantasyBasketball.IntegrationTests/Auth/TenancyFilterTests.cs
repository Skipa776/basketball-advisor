using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Auth;

public sealed class TenancyFilterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:17").Build();
    private DbContextOptions<FantasyDbContext> options = null!;
    private readonly Guid userA = Guid.NewGuid();
    private readonly Guid userB = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        options = new DbContextOptionsBuilder<FantasyDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var database = Database(userA);
        await database.Database.MigrateAsync(
            TestContext.Current.CancellationToken);
        database.Users.AddRange(User(userA, "a"), User(userB, "b"));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task U03_owned_query_without_user_context_throws()
    {
        await using var database = new FantasyDbContext(
            options,
            MissingUserContext.Instance);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            database.FantasyLeagues.ToArrayAsync(
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task U04_shared_player_is_one_row_visible_to_both_users()
    {
        var playerId = Guid.NewGuid();
        await using (var first = Database(userA))
        {
            first.Players.Add(PlayerRow.Create(
                playerId,
                "Shared Player",
                "shared player",
                ["G"],
                null));
            await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var second = Database(userB);
        var players = await second.Players
            .Where(player => player.Id == playerId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        players.Length.ShouldBe(1);
    }

    [Fact]
    public async Task U05_personal_adjustment_is_isolated_and_global_is_shared()
    {
        var playerId = Guid.NewGuid();
        var baselineId = Guid.NewGuid();
        var globalId = Guid.NewGuid();
        var personalId = Guid.NewGuid();
        await using (var first = Database(userA))
        {
            first.Players.Add(PlayerRow.Create(
                playerId,
                "Projection Player",
                "projection player",
                ["G"],
                null));
            first.BaselineProjections.Add(BaselineProjectionRow.Create(
                baselineId,
                playerId,
                20m,
                "{}",
                "{}",
                70,
                DateTimeOffset.UnixEpoch,
                "auth-test"));
            first.AdjustedProjections.AddRange(
                AdjustedProjectionRow.Create(
                    globalId,
                    null,
                    playerId,
                    baselineId,
                    "{}",
                    [],
                    0m,
                    "Moderate",
                    1m,
                    false,
                    DateTimeOffset.UnixEpoch),
                AdjustedProjectionRow.Create(
                    personalId,
                    userA,
                    playerId,
                    baselineId,
                    "{}",
                    [],
                    0m,
                    "Moderate",
                    1m,
                    false,
                    DateTimeOffset.UnixEpoch.AddMinutes(1)));
            await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var asA = Database(userA);
        (await asA.AdjustedProjections
            .Select(value => value.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken))
            .ShouldBe([globalId, personalId], ignoreOrder: true);
        await using var asB = Database(userB);
        (await asB.AdjustedProjections
            .Select(value => value.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken))
            .ShouldBe([globalId]);
    }

    [Fact]
    public void U15_every_owned_type_has_a_marker_applied_query_filter()
    {
        using var database = Database(userA);
        var ownedTypes = database.Model.GetEntityTypes()
            .Where(entityType =>
                typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .ToArray();

        ownedTypes.Length.ShouldBeGreaterThan(0);
        foreach (var entityType in ownedTypes)
        {
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty();
        }
    }

    private FantasyDbContext Database(Guid userId) =>
        new(options, new FixedUserContext(userId));

    private static FantasyUser User(Guid id, string suffix) =>
        new()
        {
            Id = id,
            UserName = $"user-{suffix}",
            NormalizedUserName = $"USER-{suffix.ToUpperInvariant()}",
            DisplayName = $"User {suffix}",
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    private sealed class FixedUserContext(Guid userId) : IUserContext
    {
        public Guid CurrentUserId => userId;
    }
}

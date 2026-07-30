using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Domain.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence;

public sealed class FantasyDbContext(DbContextOptions<FantasyDbContext> options)
    : IdentityDbContext<FantasyUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PlayerRow> Players => Set<PlayerRow>();

    public DbSet<NbaTeamRow> NbaTeams => Set<NbaTeamRow>();

    public DbSet<NbaTeamSourceRow> NbaTeamSources => Set<NbaTeamSourceRow>();

    public DbSet<ExternalPlayerIdentityRow> ExternalPlayerIdentities =>
        Set<ExternalPlayerIdentityRow>();

    public DbSet<PendingIdentityMatchRow> PendingIdentityMatches =>
        Set<PendingIdentityMatchRow>();

    public DbSet<DataImportRunRow> DataImportRuns => Set<DataImportRunRow>();

    public DbSet<FantasyLeagueRow> FantasyLeagues => Set<FantasyLeagueRow>();

    public DbSet<ScoringRuleRow> ScoringRules => Set<ScoringRuleRow>();

    public DbSet<RosterSlotRow> RosterSlots => Set<RosterSlotRow>();

    public DbSet<SeasonStatLineRow> SeasonStatLines => Set<SeasonStatLineRow>();

    public DbSet<NbaGameRow> NbaGames => Set<NbaGameRow>();

    public DbSet<AdpEntryRow> AdpEntries => Set<AdpEntryRow>();

    public DbSet<BaselineProjectionRow> BaselineProjections =>
        Set<BaselineProjectionRow>();

    public DbSet<ObservedStatsRow> ObservedStats => Set<ObservedStatsRow>();

    public DbSet<DraftSessionRow> DraftSessions => Set<DraftSessionRow>();

    public DbSet<DraftPickRow> DraftPicks => Set<DraftPickRow>();

    public DbSet<ContextEventRow> ContextEvents => Set<ContextEventRow>();

    public DbSet<PlayerContextImpactRow> PlayerContextImpacts =>
        Set<PlayerContextImpactRow>();

    public DbSet<AdjustedProjectionRow> AdjustedProjections =>
        Set<AdjustedProjectionRow>();

    public DbSet<FantasyValueRow> FantasyValues => Set<FantasyValueRow>();

    public DbSet<RecommendationRow> Recommendations => Set<RecommendationRow>();

    public DbSet<RecommendationEvidenceRow> RecommendationEvidence =>
        Set<RecommendationEvidenceRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FantasyDbContext).Assembly);
        ConfigureIdentityTables(modelBuilder);
        ConfigureNullableOwnership(modelBuilder);
        ConfigureSnakeCaseIdentityColumns(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectImmutableUpdates();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RejectImmutableUpdates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectImmutableUpdates()
    {
        if (ChangeTracker.Entries<BaselineProjectionRow>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Baseline projections are append-only.");
        }

        if (ChangeTracker.Entries<ObservedStatsRow>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Observed stats are append-only.");
        }

        if (ChangeTracker.Entries<DataImportRunRow>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Data import runs are append-only.");
        }
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FantasyUser>(builder =>
        {
            builder.ToTable("fantasy_user");
            builder.Property(user => user.DisplayName).IsRequired();
            builder.Property(user => user.CreatedAt).HasColumnType("timestamptz");
            builder.HasIndex(user => user.IsInstanceOwner)
                .IsUnique()
                .HasFilter("\"is_instance_owner\" = TRUE");
        });
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("identity_role");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claim");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_login");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_token");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("identity_role_claim");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("identity_user_role");
    }

    private static void ConfigureNullableOwnership(ModelBuilder modelBuilder)
    {
        var ownedTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType =>
                typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .ToArray();
        foreach (var entityType in ownedTypes)
        {
            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.Property(nameof(IOwnedResource.OwnerId))
                .HasColumnName("owner_id");
            builder.HasOne(typeof(FantasyUser))
                .WithMany()
                .HasForeignKey(nameof(IOwnedResource.OwnerId))
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    private static void ConfigureSnakeCaseIdentityColumns(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(entityType =>
                entityType.ClrType.Namespace == typeof(FantasyUser).Namespace
                || entityType.ClrType.Namespace == typeof(IdentityUser<Guid>).Namespace))
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string value) =>
        string.Concat(value.Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? $"_{char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));
}

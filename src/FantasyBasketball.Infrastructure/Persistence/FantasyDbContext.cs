using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence;

public sealed class FantasyDbContext(DbContextOptions<FantasyDbContext> options)
    : DbContext(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FantasyDbContext).Assembly);
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
}

using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FantasyBasketball.Infrastructure.Persistence.Configurations;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<PlayerRow>
{
    public void Configure(EntityTypeBuilder<PlayerRow> builder)
    {
        builder.ToTable("player");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.FullName).HasColumnName("full_name").IsRequired();
        builder.Property(value => value.NormalizedName).HasColumnName("normalized_name").IsRequired();
        builder.Property(value => value.CurrentTeamId).HasColumnName("current_team_id");
        builder.Property(value => value.Positions).HasColumnName("positions").HasColumnType("text[]");
        builder.Property(value => value.BirthDate).HasColumnName("birth_date");
        builder.HasIndex(value => value.NormalizedName).HasDatabaseName("ix_player_normalized_name");
        builder.HasOne<NbaTeamRow>()
            .WithMany()
            .HasForeignKey(value => value.CurrentTeamId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class NbaTeamConfiguration : IEntityTypeConfiguration<NbaTeamRow>
{
    public void Configure(EntityTypeBuilder<NbaTeamRow> builder)
    {
        builder.ToTable("nba_team");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.Name).HasColumnName("name").IsRequired();
        builder.Property(value => value.Abbreviation).HasColumnName("abbreviation").IsRequired();
    }
}

public sealed class ExternalPlayerIdentityConfiguration
    : IEntityTypeConfiguration<ExternalPlayerIdentityRow>
{
    public void Configure(EntityTypeBuilder<ExternalPlayerIdentityRow> builder)
    {
        builder.ToTable("external_player_identity");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.Provider).HasColumnName("provider").IsRequired();
        builder.Property(value => value.ExternalId).HasColumnName("external_id").IsRequired();
        builder.Property(value => value.LinkedAt).HasColumnName("linked_at").HasColumnType("timestamptz");
        builder.Property(value => value.ConfirmedByHuman).HasColumnName("confirmed_by_human");
        builder.HasIndex(value => new { value.Provider, value.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_external_player_identity_provider_external_id");
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FantasyLeagueConfiguration : IEntityTypeConfiguration<FantasyLeagueRow>
{
    public void Configure(EntityTypeBuilder<FantasyLeagueRow> builder)
    {
        builder.ToTable("fantasy_league");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.Name).HasColumnName("name").IsRequired();
        builder.Property(value => value.Type).HasColumnName("type").IsRequired();
        builder.Property(value => value.TeamCount).HasColumnName("team_count");
        builder.Property(value => value.Categories).HasColumnName("categories").HasColumnType("text[]");
        builder.Property(value => value.Cadence).HasColumnName("cadence").IsRequired();
    }
}

public sealed class ScoringRuleConfiguration : IEntityTypeConfiguration<ScoringRuleRow>
{
    public void Configure(EntityTypeBuilder<ScoringRuleRow> builder)
    {
        builder.ToTable("scoring_rule");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.FantasyLeagueId).HasColumnName("fantasy_league_id");
        builder.Property(value => value.Stat).HasColumnName("stat").IsRequired();
        builder.Property(value => value.PointsPerUnit)
            .HasColumnName("points_per_unit")
            .HasPrecision(8, 4);
        builder.Property(value => value.Ordinal).HasColumnName("ordinal");
        builder.HasOne<FantasyLeagueRow>()
            .WithMany(value => value.ScoringRules)
            .HasForeignKey(value => value.FantasyLeagueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RosterSlotConfiguration : IEntityTypeConfiguration<RosterSlotRow>
{
    public void Configure(EntityTypeBuilder<RosterSlotRow> builder)
    {
        builder.ToTable("roster_slot");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.FantasyLeagueId).HasColumnName("fantasy_league_id");
        builder.Property(value => value.Kind).HasColumnName("kind").IsRequired();
        builder.Property(value => value.Ordinal).HasColumnName("ordinal");
        builder.HasOne<FantasyLeagueRow>()
            .WithMany(value => value.RosterSlots)
            .HasForeignKey(value => value.FantasyLeagueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SeasonStatLineConfiguration : IEntityTypeConfiguration<SeasonStatLineRow>
{
    public void Configure(EntityTypeBuilder<SeasonStatLineRow> builder)
    {
        builder.ToTable("season_stat_line");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.SeasonEndYear).HasColumnName("season_end_year");
        builder.Property(value => value.GamesPlayed).HasColumnName("games_played");
        builder.Property(value => value.MinutesPerGame).HasColumnName("minutes_per_game").HasPrecision(10, 4);
        builder.Property(value => value.PerGame).HasColumnName("per_game").HasColumnType("jsonb");
        builder.Property(value => value.Totals).HasColumnName("totals").HasColumnType("jsonb");
        builder.Property(value => value.UsageRate).HasColumnName("usage_rate").HasPrecision(10, 4);
        builder.Property(value => value.Source).HasColumnName("source").IsRequired();
        builder.Property(value => value.ExternalId).HasColumnName("external_id");
        builder.Property(value => value.FetchedAt).HasColumnName("fetched_at").HasColumnType("timestamptz");
        builder.Property(value => value.SourceTimestamp)
            .HasColumnName("source_timestamp")
            .HasColumnType("timestamptz");
        builder.Property(value => value.ParserVersion).HasColumnName("parser_version").IsRequired();
        builder.Property(value => value.Confidence).HasColumnName("confidence").HasPrecision(10, 4);
        builder.Property(value => value.RawRecordHash).HasColumnName("raw_record_hash").IsRequired();
        builder.HasIndex(value => new { value.PlayerId, value.SeasonEndYear, value.Source })
            .IsUnique()
            .HasDatabaseName("ux_season_stat_line_player_season_source");
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BaselineProjectionConfiguration
    : IEntityTypeConfiguration<BaselineProjectionRow>
{
    public void Configure(EntityTypeBuilder<BaselineProjectionRow> builder)
    {
        builder.ToTable("baseline_projection");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.ProjectedMinutesPerGame)
            .HasColumnName("projected_minutes_per_game")
            .HasPrecision(10, 4);
        builder.Property(value => value.PerMinuteRates)
            .HasColumnName("per_minute_rates")
            .HasColumnType("jsonb");
        builder.Property(value => value.ProjectedPerGame)
            .HasColumnName("projected_per_game")
            .HasColumnType("jsonb");
        builder.Property(value => value.ProjectedGamesPlayed).HasColumnName("projected_games_played");
        builder.Property(value => value.ComputedAt).HasColumnName("computed_at").HasColumnType("timestamptz");
        builder.Property(value => value.ModelVersion).HasColumnName("model_version").IsRequired();
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DraftSessionConfiguration : IEntityTypeConfiguration<DraftSessionRow>
{
    public void Configure(EntityTypeBuilder<DraftSessionRow> builder)
    {
        builder.ToTable("draft_session");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.FantasyLeagueId).HasColumnName("fantasy_league_id");
        builder.Property(value => value.RoundCount).HasColumnName("round_count");
        builder.HasOne<FantasyLeagueRow>()
            .WithMany()
            .HasForeignKey(value => value.FantasyLeagueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DraftPickConfiguration : IEntityTypeConfiguration<DraftPickRow>
{
    public void Configure(EntityTypeBuilder<DraftPickRow> builder)
    {
        builder.ToTable("draft_pick");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.DraftSessionId).HasColumnName("draft_session_id");
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.PickNumber).HasColumnName("pick_number");
        builder.HasIndex(value => new { value.DraftSessionId, value.PickNumber })
            .IsUnique()
            .HasDatabaseName("ux_draft_pick_session_pick_number");
        builder.HasOne<DraftSessionRow>()
            .WithMany()
            .HasForeignKey(value => value.DraftSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

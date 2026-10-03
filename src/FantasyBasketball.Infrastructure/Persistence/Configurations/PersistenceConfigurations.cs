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
        builder.HasIndex(value => value.Abbreviation)
            .IsUnique()
            .HasDatabaseName("ux_nba_team_abbreviation");
    }
}

public sealed class NbaTeamSourceConfiguration : IEntityTypeConfiguration<NbaTeamSourceRow>
{
    public void Configure(EntityTypeBuilder<NbaTeamSourceRow> builder)
    {
        builder.ToTable("nba_team_source");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.NbaTeamId).HasColumnName("nba_team_id");
        builder.Property(value => value.Source).HasColumnName("source").IsRequired();
        builder.Property(value => value.ExternalId).HasColumnName("external_id").IsRequired();
        builder.Property(value => value.FetchedAt)
            .HasColumnName("fetched_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.SourceTimestamp)
            .HasColumnName("source_timestamp")
            .HasColumnType("timestamptz");
        builder.Property(value => value.ParserVersion)
            .HasColumnName("parser_version")
            .IsRequired();
        builder.Property(value => value.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(10, 4);
        builder.Property(value => value.RawRecordHash)
            .HasColumnName("raw_record_hash")
            .IsRequired();
        builder.HasIndex(value => new { value.Source, value.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_nba_team_source_source_external_id");
        builder.HasOne<NbaTeamRow>()
            .WithMany()
            .HasForeignKey(value => value.NbaTeamId)
            .OnDelete(DeleteBehavior.Restrict);
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
        builder.HasIndex(value => new { value.PlayerId, value.Provider })
            .IsUnique()
            .HasDatabaseName("ux_external_player_identity_player_provider");
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PendingIdentityMatchConfiguration
    : IEntityTypeConfiguration<PendingIdentityMatchRow>
{
    public void Configure(EntityTypeBuilder<PendingIdentityMatchRow> builder)
    {
        builder.ToTable("pending_identity_match");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.Provider).HasColumnName("provider").IsRequired();
        builder.Property(value => value.ExternalId).HasColumnName("external_id").IsRequired();
        builder.Property(value => value.FullName).HasColumnName("full_name").IsRequired();
        builder.Property(value => value.NormalizedName)
            .HasColumnName("normalized_name")
            .IsRequired();
        builder.Property(value => value.CandidatePlayerIds)
            .HasColumnName("candidate_player_ids")
            .HasColumnType("uuid[]");
        builder.Property(value => value.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.Reason).HasColumnName("reason").IsRequired();
        builder.HasIndex(value => new { value.Provider, value.ExternalId })
            .HasDatabaseName("ix_pending_identity_match_provider_external_id");
    }
}

public sealed class DataImportRunConfiguration : IEntityTypeConfiguration<DataImportRunRow>
{
    public void Configure(EntityTypeBuilder<DataImportRunRow> builder)
    {
        builder.ToTable("data_import_run");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.Source).HasColumnName("source").IsRequired();
        builder.Property(value => value.Status).HasColumnName("status").IsRequired();
        builder.Property(value => value.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.FinishedAt)
            .HasColumnName("finished_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.RowsWritten).HasColumnName("rows_written");
        builder.Property(value => value.PendingIdentityMatches)
            .HasColumnName("pending_identity_matches");
        builder.Property(value => value.FailureDetail).HasColumnName("failure_detail");
        builder.HasIndex(value => new { value.Source, value.StartedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_data_import_run_source_started_at");
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
        builder.Property(value => value.WeeklyAcquisitionLimit).HasColumnName("weekly_acquisition_limit").HasDefaultValue(7);
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

public sealed class PlayerAvailabilityConfiguration : IEntityTypeConfiguration<PlayerAvailabilityRow>
{
    public void Configure(EntityTypeBuilder<PlayerAvailabilityRow> builder)
    {
        builder.ToTable("player_availability");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.Status).HasColumnName("status").IsRequired();
        builder.Property(value => value.BodyPart).HasColumnName("body_part");
        builder.Property(value => value.Notes).HasColumnName("notes");
        builder.Property(value => value.ReportedAt).HasColumnName("reported_at");
        builder.Property(value => value.Source).HasColumnName("source").IsRequired();
        builder.Property(value => value.ExternalId).HasColumnName("external_id");
        builder.Property(value => value.FetchedAt).HasColumnName("fetched_at");
        builder.Property(value => value.SourceTimestamp).HasColumnName("source_timestamp");
        builder.Property(value => value.ParserVersion).HasColumnName("parser_version").IsRequired();
        builder.Property(value => value.Confidence).HasColumnName("confidence").HasPrecision(10, 4);
        builder.Property(value => value.RawRecordHash).HasColumnName("raw_record_hash").IsRequired();
        builder.HasOne<PlayerRow>().WithMany().HasForeignKey(value => value.PlayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.PlayerId, value.Source }).IsUnique();
    }
}

public sealed class LeagueEligibilityConfiguration : IEntityTypeConfiguration<LeagueEligibilityRow>
{
    public void Configure(EntityTypeBuilder<LeagueEligibilityRow> builder)
    {
        builder.ToTable("league_player_eligibility");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.FantasyLeagueId).HasColumnName("fantasy_league_id");
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.Positions).HasColumnName("positions").IsRequired();
        builder.HasOne<FantasyLeagueRow>()
            .WithMany()
            .HasForeignKey(value => value.FantasyLeagueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.FantasyLeagueId, value.PlayerId }).IsUnique();
    }
}

public sealed class LeagueTeamConfiguration : IEntityTypeConfiguration<LeagueTeamRow>
{
    public void Configure(EntityTypeBuilder<LeagueTeamRow> builder)
    {
        builder.ToTable("league_team");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.FantasyLeagueId).HasColumnName("fantasy_league_id");
        builder.Property(value => value.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(value => value.IsUsersTeam).HasColumnName("is_users_team");
        builder.Property(value => value.Ordinal).HasColumnName("ordinal");
        builder.HasOne<FantasyLeagueRow>()
            .WithMany()
            .HasForeignKey(value => value.FantasyLeagueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(value => value.Entries)
            .WithOne()
            .HasForeignKey(value => value.LeagueTeamId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.FantasyLeagueId, value.Ordinal }).IsUnique();
    }
}

public sealed class LeagueRosterEntryConfiguration : IEntityTypeConfiguration<LeagueRosterEntryRow>
{
    public void Configure(EntityTypeBuilder<LeagueRosterEntryRow> builder)
    {
        builder.ToTable("league_roster_entry");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.LeagueTeamId).HasColumnName("league_team_id");
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.Ordinal).HasColumnName("ordinal");
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.LeagueTeamId, value.PlayerId }).IsUnique();
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
        builder.Property(value => value.Age).HasColumnName("age");
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

public sealed class NbaGameConfiguration : IEntityTypeConfiguration<NbaGameRow>
{
    public void Configure(EntityTypeBuilder<NbaGameRow> builder)
    {
        builder.ToTable("nba_game");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.SeasonEndYear).HasColumnName("season_end_year");
        builder.Property(value => value.StartsAt)
            .HasColumnName("starts_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.HomeTeamId).HasColumnName("home_team_id");
        builder.Property(value => value.AwayTeamId).HasColumnName("away_team_id");
        builder.Property(value => value.HomeScore).HasColumnName("home_score");
        builder.Property(value => value.AwayScore).HasColumnName("away_score");
        builder.Property(value => value.Status).HasColumnName("status").IsRequired();
        builder.Property(value => value.Source).HasColumnName("source").IsRequired();
        builder.Property(value => value.ExternalId).HasColumnName("external_id").IsRequired();
        builder.Property(value => value.FetchedAt)
            .HasColumnName("fetched_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.SourceTimestamp)
            .HasColumnName("source_timestamp")
            .HasColumnType("timestamptz");
        builder.Property(value => value.ParserVersion)
            .HasColumnName("parser_version")
            .IsRequired();
        builder.Property(value => value.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(10, 4);
        builder.Property(value => value.RawRecordHash)
            .HasColumnName("raw_record_hash")
            .IsRequired();
        builder.HasIndex(value => new { value.Source, value.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_nba_game_source_external_id");
        builder.HasOne<NbaTeamRow>()
            .WithMany()
            .HasForeignKey(value => value.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NbaTeamRow>()
            .WithMany()
            .HasForeignKey(value => value.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AdpEntryConfiguration : IEntityTypeConfiguration<AdpEntryRow>
{
    public void Configure(EntityTypeBuilder<AdpEntryRow> builder)
    {
        builder.ToTable("adp_entry");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.AverageDraftPosition)
            .HasColumnName("average_draft_position")
            .HasPrecision(10, 4);
        builder.Property(value => value.StandardDeviation)
            .HasColumnName("standard_deviation")
            .HasPrecision(10, 4);
        builder.Property(value => value.Source).HasColumnName("source").IsRequired();
        builder.Property(value => value.ExternalId)
            .HasColumnName("external_id")
            .IsRequired();
        builder.Property(value => value.FetchedAt)
            .HasColumnName("fetched_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.SourceTimestamp)
            .HasColumnName("source_timestamp")
            .HasColumnType("timestamptz");
        builder.Property(value => value.ParserVersion)
            .HasColumnName("parser_version")
            .IsRequired();
        builder.Property(value => value.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(10, 4);
        builder.Property(value => value.RawRecordHash)
            .HasColumnName("raw_record_hash")
            .IsRequired();
        builder.HasIndex(value => new { value.PlayerId, value.FetchedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_adp_entry_player_fetched_at");
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ModelVersionConfiguration
    : IEntityTypeConfiguration<ModelVersionRow>
{
    public void Configure(EntityTypeBuilder<ModelVersionRow> builder)
    {
        builder.ToTable("model_version");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.ModelName).HasColumnName("model_name").IsRequired();
        builder.Property(value => value.Version).HasColumnName("version").IsRequired();
        builder.Property(value => value.FittedAt)
            .HasColumnName("fitted_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.TrainSeasonEndYears)
            .HasColumnName("train_season_end_years")
            .HasColumnType("integer[]");
        builder.Property(value => value.Parameters).HasColumnName("parameters").HasColumnType("jsonb");
        builder.Property(value => value.Metrics).HasColumnName("metrics").HasColumnType("jsonb");
        builder.Property(value => value.CardMarkdown).HasColumnName("card_markdown").IsRequired();
        builder.Property(value => value.IsActive).HasColumnName("is_active");
        builder.HasIndex(value => new { value.ModelName, value.Version })
            .IsUnique()
            .HasDatabaseName("ux_model_version_name_version");
        builder.HasIndex(value => value.ModelName)
            .IsUnique()
            .HasFilter("\"is_active\"")
            .HasDatabaseName("ux_model_version_active_per_model");
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
        builder.Property(value => value.ObservedStatsId).HasColumnName("observed_stats_id");
        builder.HasOne<ObservedStatsRow>().WithMany()
            .HasForeignKey(value => value.ObservedStatsId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ObservedStatsConfiguration
    : IEntityTypeConfiguration<ObservedStatsRow>
{
    public void Configure(EntityTypeBuilder<ObservedStatsRow> builder)
    {
        builder.ToTable("observed_stats");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.SeasonEndYear).HasColumnName("season_end_year");
        builder.Property(value => value.Source).HasColumnName("source").IsRequired();
        builder.Property(value => value.AsOf)
            .HasColumnName("as_of")
            .HasColumnType("timestamptz");
        builder.HasIndex(value => new { value.PlayerId, value.AsOf })
            .IsDescending(false, true)
            .HasDatabaseName("ix_observed_stats_player_as_of");
        builder.HasOne<SeasonStatLineRow>()
            .WithMany()
            .HasForeignKey(value => new
            {
                value.PlayerId,
                value.SeasonEndYear,
                value.Source,
            })
            .HasPrincipalKey(value => new
            {
                value.PlayerId,
                value.SeasonEndYear,
                value.Source,
            })
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
        builder.Property(value => value.TeamCount).HasColumnName("team_count");
        builder.Property(value => value.UserSlot).HasColumnName("user_slot");
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

public sealed class ContextEventConfiguration
    : IEntityTypeConfiguration<ContextEventRow>
{
    public void Configure(EntityTypeBuilder<ContextEventRow> builder)
    {
        builder.ToTable("context_event");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.Type).HasColumnName("type").IsRequired();
        builder.Property(value => value.TeamId).HasColumnName("team_id");
        builder.Property(value => value.PrimaryPlayerId).HasColumnName("primary_player_id");
        builder.Property(value => value.AffectedPlayerIds)
            .HasColumnName("affected_player_ids")
            .HasColumnType("uuid[]");
        builder.Property(value => value.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("timestamptz");
        builder.Property(value => value.ExpectedExpiration)
            .HasColumnName("expected_expiration")
            .HasColumnType("timestamptz");
        builder.Property(value => value.Direction).HasColumnName("direction").IsRequired();
        builder.Property(value => value.Magnitude)
            .HasColumnName("magnitude")
            .HasPrecision(10, 4);
        builder.Property(value => value.Confidence)
            .HasColumnName("confidence")
            .IsRequired();
        builder.Property(value => value.SourceUrl).HasColumnName("source_url");
        builder.Property(value => value.SourceName)
            .HasColumnName("source_name")
            .IsRequired();
        builder.Property(value => value.RawText).HasColumnName("raw_text");
        builder.Property(value => value.Summary).HasColumnName("summary").IsRequired();
        builder.Property(value => value.Verification)
            .HasColumnName("verification")
            .IsRequired();
        builder.Property(value => value.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id");
        builder.Property(value => value.VerifiedByUserId)
            .HasColumnName("verified_by_user_id");
        builder.Property(value => value.VerifiedAt)
            .HasColumnName("verified_at")
            .HasColumnType("timestamptz");
        builder.Property(value => value.ReviewedAt)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamptz");
        builder.HasIndex(value => new
        {
            value.EffectiveFrom,
            value.ExpectedExpiration,
        }).HasDatabaseName("ix_context_event_effective_expiration");
        builder.HasOne<NbaTeamRow>()
            .WithMany()
            .HasForeignKey(value => value.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PrimaryPlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlayerContextImpactConfiguration
    : IEntityTypeConfiguration<PlayerContextImpactRow>
{
    public void Configure(EntityTypeBuilder<PlayerContextImpactRow> builder)
    {
        builder.ToTable("player_context_impact");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.ContextEventId).HasColumnName("context_event_id");
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.MinutesDelta)
            .HasColumnName("minutes_delta")
            .HasPrecision(10, 4);
        builder.Property(value => value.UsageDelta)
            .HasColumnName("usage_delta")
            .HasPrecision(10, 4);
        builder.Property(value => value.AssistShareDelta)
            .HasColumnName("assist_share_delta")
            .HasPrecision(10, 4);
        builder.Property(value => value.ReboundShareDelta)
            .HasColumnName("rebound_share_delta")
            .HasPrecision(10, 4);
        builder.Property(value => value.ShotVolumeDelta)
            .HasColumnName("shot_volume_delta")
            .HasPrecision(10, 4);
        builder.Property(value => value.RoleRiskDelta)
            .HasColumnName("role_risk_delta")
            .HasPrecision(10, 4);
        builder.Property(value => value.ProjectionConfidenceDelta)
            .HasColumnName("projection_confidence_delta")
            .HasPrecision(10, 4);
        builder.Property(value => value.IsOverridden).HasColumnName("is_overridden");
        builder.Property(value => value.OverrideUserId).HasColumnName("override_user_id");
        builder.Property(value => value.OverriddenAt)
            .HasColumnName("overridden_at")
            .HasColumnType("timestamptz");
        builder.HasIndex(value => new { value.ContextEventId, value.PlayerId })
            .IsUnique()
            .HasDatabaseName("ux_context_impact_event_player");
        builder.HasOne<ContextEventRow>()
            .WithMany(value => value.Impacts)
            .HasForeignKey(value => value.ContextEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AdjustedProjectionConfiguration
    : IEntityTypeConfiguration<AdjustedProjectionRow>
{
    public void Configure(EntityTypeBuilder<AdjustedProjectionRow> builder)
    {
        builder.ToTable("adjusted_projection");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.BaselineProjectionId)
            .HasColumnName("baseline_projection_id");
        builder.Property(value => value.ProjectedPerGame)
            .HasColumnName("projected_per_game")
            .HasColumnType("jsonb");
        builder.Property(value => value.AppliedContextEventIds)
            .HasColumnName("applied_context_event_ids")
            .HasColumnType("uuid[]");
        builder.Property(value => value.RoleRisk)
            .HasColumnName("role_risk")
            .HasPrecision(10, 4);
        builder.Property(value => value.Confidence)
            .HasColumnName("confidence")
            .IsRequired();
        builder.Property(value => value.ContextCertainty)
            .HasColumnName("context_certainty")
            .HasPrecision(10, 4);
        builder.Property(value => value.HasUnverifiedContext)
            .HasColumnName("has_unverified_context");
        builder.Property(value => value.ComputedAt)
            .HasColumnName("computed_at")
            .HasColumnType("timestamptz");
        builder.HasIndex(value => new { value.PlayerId, value.ComputedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_adjusted_projection_player_computed_at");
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BaselineProjectionRow>()
            .WithMany()
            .HasForeignKey(value => value.BaselineProjectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FantasyValueConfiguration
    : IEntityTypeConfiguration<FantasyValueRow>
{
    public void Configure(EntityTypeBuilder<FantasyValueRow> builder)
    {
        builder.ToTable("fantasy_value");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerId).HasColumnName("player_id");
        builder.Property(value => value.FantasyLeagueId)
            .HasColumnName("fantasy_league_id");
        builder.Property(value => value.PerGame)
            .HasColumnName("per_game")
            .HasPrecision(10, 4);
        builder.Property(value => value.SeasonTotal)
            .HasColumnName("season_total")
            .HasPrecision(10, 4);
        builder.Property(value => value.AdjustedProjectionId)
            .HasColumnName("adjusted_projection_id");
        builder.Property(value => value.ComputedAt)
            .HasColumnName("computed_at").HasColumnType("timestamptz");
        builder.Property(value => value.ScoringProfile).HasColumnName("scoring_profile");
        builder.Property(value => value.PublicationId).HasColumnName("publication_id");
        builder.HasIndex(value => new { value.FantasyLeagueId, value.PlayerId, value.ComputedAt })
            .IsDescending(false, false, true);
        builder.HasIndex(value => new
        {
            value.PlayerId,
            value.FantasyLeagueId,
            value.AdjustedProjectionId,
        }).HasDatabaseName("ix_fantasy_value_player_league_adjusted");
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyLeagueRow>()
            .WithMany()
            .HasForeignKey(value => value.FantasyLeagueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AdjustedProjectionRow>()
            .WithMany()
            .HasForeignKey(value => value.AdjustedProjectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RecommendationConfiguration
    : IEntityTypeConfiguration<RecommendationRow>
{
    public void Configure(EntityTypeBuilder<RecommendationRow> builder)
    {
        builder.ToTable("recommendation");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.Action).HasColumnName("action").IsRequired();
        builder.Property(value => value.SubjectPlayerId)
            .HasColumnName("subject_player_id");
        builder.Property(value => value.Score)
            .HasColumnName("score")
            .HasPrecision(10, 4);
        builder.Property(value => value.Confidence)
            .HasColumnName("confidence")
            .IsRequired();
        builder.HasOne<PlayerRow>()
            .WithMany()
            .HasForeignKey(value => value.SubjectPlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RecommendationEvidenceConfiguration
    : IEntityTypeConfiguration<RecommendationEvidenceRow>
{
    public void Configure(EntityTypeBuilder<RecommendationEvidenceRow> builder)
    {
        builder.ToTable("recommendation_evidence");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.RecommendationId)
            .HasColumnName("recommendation_id");
        builder.Property(value => value.Kind).HasColumnName("kind").IsRequired();
        builder.Property(value => value.Polarity)
            .HasColumnName("polarity")
            .IsRequired();
        builder.Property(value => value.Statement)
            .HasColumnName("statement")
            .IsRequired();
        builder.Property(value => value.Magnitude)
            .HasColumnName("magnitude")
            .HasPrecision(10, 4);
        builder.Property(value => value.Ordinal).HasColumnName("ordinal");
        builder.HasIndex(value => new { value.RecommendationId, value.Ordinal })
            .IsUnique()
            .HasDatabaseName("ux_recommendation_evidence_ordinal");
        builder.HasOne<RecommendationRow>()
            .WithMany(value => value.Evidence)
            .HasForeignKey(value => value.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

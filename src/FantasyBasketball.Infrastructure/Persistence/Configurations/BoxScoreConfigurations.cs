using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FantasyBasketball.Infrastructure.Persistence.Configurations;

public sealed class BoxScoreSnapshotConfiguration : IEntityTypeConfiguration<BoxScoreSnapshotRow>
{
    public void Configure(EntityTypeBuilder<BoxScoreSnapshotRow> builder)
    {
        builder.ToTable("box_score_snapshot");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.GameId).HasColumnName("game_id");
        builder.Property(row => row.SeasonEndYear).HasColumnName("season_end_year");
        builder.Property(row => row.PlayedOn).HasColumnName("played_on");
        builder.Property(row => row.Phase).HasColumnName("phase").IsRequired();
        builder.Property(row => row.Source).HasColumnName("source").IsRequired();
        builder.Property(row => row.ExternalId).HasColumnName("external_id");
        builder.Property(row => row.FetchedAt).HasColumnName("fetched_at").HasColumnType("timestamptz");
        builder.Property(row => row.SourceTimestamp).HasColumnName("source_timestamp").HasColumnType("timestamptz");
        builder.Property(row => row.ParserVersion).HasColumnName("parser_version").IsRequired();
        builder.Property(row => row.Confidence).HasColumnName("confidence").HasPrecision(10, 4);
        builder.Property(row => row.RawRecordHash).HasColumnName("raw_record_hash").IsRequired();
        builder.HasIndex(row => new { row.GameId, row.Source, row.ParserVersion, row.RawRecordHash, row.Phase })
            .IsUnique().HasDatabaseName("ux_box_score_snapshot_content");
        builder.HasIndex(row => new { row.SeasonEndYear, row.Source, row.GameId, row.FetchedAt })
            .IsDescending(false, false, false, true).HasDatabaseName("ix_box_score_snapshot_season_latest");
        builder.HasOne<NbaGameRow>().WithMany().HasForeignKey(row => row.GameId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlayerGameStatConfiguration : IEntityTypeConfiguration<PlayerGameStatRow>
{
    public void Configure(EntityTypeBuilder<PlayerGameStatRow> builder)
    {
        builder.ToTable("player_game_stat", table => table.HasCheckConstraint(
            "ck_player_game_stat_appearance", "(did_play AND statistics IS NOT NULL) OR (NOT did_play AND statistics IS NULL)"));
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.SnapshotId).HasColumnName("snapshot_id");
        builder.Property(row => row.PlayerId).HasColumnName("player_id");
        builder.Property(row => row.DidPlay).HasColumnName("did_play");
        builder.Property(row => row.Statistics).HasColumnName("statistics").HasColumnType("jsonb");
        builder.Property(row => row.Source).HasColumnName("source").IsRequired();
        builder.Property(row => row.ExternalId).HasColumnName("external_id");
        builder.Property(row => row.FetchedAt).HasColumnName("fetched_at").HasColumnType("timestamptz");
        builder.Property(row => row.SourceTimestamp).HasColumnName("source_timestamp").HasColumnType("timestamptz");
        builder.Property(row => row.ParserVersion).HasColumnName("parser_version").IsRequired();
        builder.Property(row => row.Confidence).HasColumnName("confidence").HasPrecision(10, 4);
        builder.Property(row => row.RawRecordHash).HasColumnName("raw_record_hash").IsRequired();
        builder.HasIndex(row => new { row.SnapshotId, row.PlayerId }).IsUnique().HasDatabaseName("ux_player_game_stat_snapshot_player");
        builder.HasIndex(row => row.PlayerId).HasDatabaseName("ix_player_game_stat_player");
        builder.HasOne<BoxScoreSnapshotRow>().WithMany().HasForeignKey(row => row.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlayerRow>().WithMany().HasForeignKey(row => row.PlayerId).OnDelete(DeleteBehavior.Restrict);
    }
}

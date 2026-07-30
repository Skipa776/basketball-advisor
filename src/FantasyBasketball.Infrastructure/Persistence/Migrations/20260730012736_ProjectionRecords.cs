using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_season_stat_line_player_id_season_end_year_source",
                table: "season_stat_line",
                columns: new[] { "player_id", "season_end_year", "source" });

            migrationBuilder.CreateTable(
                name: "observed_stats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_end_year = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    as_of = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observed_stats", x => x.id);
                    table.ForeignKey(
                        name: "FK_observed_stats_season_stat_line_player_id_season_end_year_s~",
                        columns: x => new { x.player_id, x.season_end_year, x.source },
                        principalTable: "season_stat_line",
                        principalColumns: new[] { "player_id", "season_end_year", "source" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_observed_stats_player_as_of",
                table: "observed_stats",
                columns: new[] { "player_id", "as_of" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_observed_stats_player_id_season_end_year_source",
                table: "observed_stats",
                columns: new[] { "player_id", "season_end_year", "source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observed_stats");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_season_stat_line_player_id_season_end_year_source",
                table: "season_stat_line");
        }
    }
}

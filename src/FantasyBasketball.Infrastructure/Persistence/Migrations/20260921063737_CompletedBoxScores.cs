using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletedBoxScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "box_score_snapshot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_end_year = table.Column<int>(type: "integer", nullable: false),
                    played_on = table.Column<DateOnly>(type: "date", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source_timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    parser_version = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    raw_record_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_box_score_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "FK_box_score_snapshot_nba_game_game_id",
                        column: x => x.game_id,
                        principalTable: "nba_game",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_game_stat",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    did_play = table.Column<bool>(type: "boolean", nullable: false),
                    statistics = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source_timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    parser_version = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    raw_record_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_game_stat", x => x.id);
                    table.CheckConstraint("ck_player_game_stat_appearance", "(did_play AND statistics IS NOT NULL) OR (NOT did_play AND statistics IS NULL)");
                    table.ForeignKey(
                        name: "FK_player_game_stat_box_score_snapshot_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "box_score_snapshot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_player_game_stat_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_box_score_snapshot_season_latest",
                table: "box_score_snapshot",
                columns: new[] { "season_end_year", "source", "game_id", "fetched_at" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ux_box_score_snapshot_content",
                table: "box_score_snapshot",
                columns: new[] { "game_id", "source", "parser_version", "raw_record_hash", "phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_game_stat_player",
                table: "player_game_stat",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ux_player_game_stat_snapshot_player",
                table: "player_game_stat",
                columns: new[] { "snapshot_id", "player_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_game_stat");

            migrationBuilder.DropTable(
                name: "box_score_snapshot");
        }
    }
}

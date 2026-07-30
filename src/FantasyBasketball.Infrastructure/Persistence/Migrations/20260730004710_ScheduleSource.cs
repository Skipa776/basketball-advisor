using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nba_game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_end_year = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    home_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    away_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    home_score = table.Column<int>(type: "integer", nullable: true),
                    away_score = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source_timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    parser_version = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    raw_record_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nba_game", x => x.id);
                    table.ForeignKey(
                        name: "FK_nba_game_nba_team_away_team_id",
                        column: x => x.away_team_id,
                        principalTable: "nba_team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_nba_game_nba_team_home_team_id",
                        column: x => x.home_team_id,
                        principalTable: "nba_team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "nba_team_source",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nba_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    source_timestamp = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    parser_version = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    raw_record_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nba_team_source", x => x.id);
                    table.ForeignKey(
                        name: "FK_nba_team_source_nba_team_nba_team_id",
                        column: x => x.nba_team_id,
                        principalTable: "nba_team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_nba_team_abbreviation",
                table: "nba_team",
                column: "abbreviation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nba_game_away_team_id",
                table: "nba_game",
                column: "away_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_nba_game_home_team_id",
                table: "nba_game",
                column: "home_team_id");

            migrationBuilder.CreateIndex(
                name: "ux_nba_game_source_external_id",
                table: "nba_game",
                columns: new[] { "source", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nba_team_source_nba_team_id",
                table: "nba_team_source",
                column: "nba_team_id");

            migrationBuilder.CreateIndex(
                name: "ux_nba_team_source_source_external_id",
                table: "nba_team_source",
                columns: new[] { "source", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nba_game");

            migrationBuilder.DropTable(
                name: "nba_team_source");

            migrationBuilder.DropIndex(
                name: "ux_nba_team_abbreviation",
                table: "nba_team");
        }
    }
}

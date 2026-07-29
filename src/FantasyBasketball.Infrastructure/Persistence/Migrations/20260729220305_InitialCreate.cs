using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fantasy_league",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    team_count = table.Column<int>(type: "integer", nullable: false),
                    categories = table.Column<string[]>(type: "text[]", nullable: false),
                    cadence = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fantasy_league", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nba_team",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    abbreviation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nba_team", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "draft_session",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fantasy_league_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_draft_session", x => x.id);
                    table.ForeignKey(
                        name: "FK_draft_session_fantasy_league_fantasy_league_id",
                        column: x => x.fantasy_league_id,
                        principalTable: "fantasy_league",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roster_slot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fantasy_league_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roster_slot", x => x.id);
                    table.ForeignKey(
                        name: "FK_roster_slot_fantasy_league_fantasy_league_id",
                        column: x => x.fantasy_league_id,
                        principalTable: "fantasy_league",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scoring_rule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fantasy_league_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stat = table.Column<string>(type: "text", nullable: false),
                    points_per_unit = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_rule", x => x.id);
                    table.ForeignKey(
                        name: "FK_scoring_rule_fantasy_league_fantasy_league_id",
                        column: x => x.fantasy_league_id,
                        principalTable: "fantasy_league",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    current_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    positions = table.Column<string[]>(type: "text[]", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player", x => x.id);
                    table.ForeignKey(
                        name: "FK_player_nba_team_current_team_id",
                        column: x => x.current_team_id,
                        principalTable: "nba_team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "baseline_projection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    projected_minutes_per_game = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    per_minute_rates = table.Column<string>(type: "jsonb", nullable: false),
                    projected_per_game = table.Column<string>(type: "jsonb", nullable: false),
                    projected_games_played = table.Column<int>(type: "integer", nullable: false),
                    model_version = table.Column<string>(type: "text", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_baseline_projection", x => x.id);
                    table.ForeignKey(
                        name: "FK_baseline_projection_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "draft_pick",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pick_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_draft_pick", x => x.id);
                    table.ForeignKey(
                        name: "FK_draft_pick_draft_session_draft_session_id",
                        column: x => x.draft_session_id,
                        principalTable: "draft_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_draft_pick_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_player_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    confirmed_by_human = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_player_identity", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_player_identity_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "season_stat_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_end_year = table.Column<int>(type: "integer", nullable: false),
                    games_played = table.Column<int>(type: "integer", nullable: false),
                    minutes_per_game = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    per_game = table.Column<string>(type: "jsonb", nullable: false),
                    totals = table.Column<string>(type: "jsonb", nullable: false),
                    usage_rate = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_season_stat_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_season_stat_line_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_baseline_projection_player_id",
                table: "baseline_projection",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_draft_pick_player_id",
                table: "draft_pick",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ux_draft_pick_session_pick_number",
                table: "draft_pick",
                columns: new[] { "draft_session_id", "pick_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_draft_session_fantasy_league_id",
                table: "draft_session",
                column: "fantasy_league_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_player_identity_player_id",
                table: "external_player_identity",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ux_external_player_identity_provider_external_id",
                table: "external_player_identity",
                columns: new[] { "provider", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_current_team_id",
                table: "player",
                column: "current_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_player_normalized_name",
                table: "player",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "IX_roster_slot_fantasy_league_id",
                table: "roster_slot",
                column: "fantasy_league_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_rule_fantasy_league_id",
                table: "scoring_rule",
                column: "fantasy_league_id");

            migrationBuilder.CreateIndex(
                name: "ux_season_stat_line_player_season_source",
                table: "season_stat_line",
                columns: new[] { "player_id", "season_end_year", "source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "baseline_projection");

            migrationBuilder.DropTable(
                name: "draft_pick");

            migrationBuilder.DropTable(
                name: "external_player_identity");

            migrationBuilder.DropTable(
                name: "roster_slot");

            migrationBuilder.DropTable(
                name: "scoring_rule");

            migrationBuilder.DropTable(
                name: "season_stat_line");

            migrationBuilder.DropTable(
                name: "draft_session");

            migrationBuilder.DropTable(
                name: "player");

            migrationBuilder.DropTable(
                name: "fantasy_league");

            migrationBuilder.DropTable(
                name: "nba_team");
        }
    }
}

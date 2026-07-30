using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContextPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adjusted_projection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_projection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    projected_per_game = table.Column<string>(type: "jsonb", nullable: false),
                    applied_context_event_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    role_risk = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    confidence = table.Column<string>(type: "text", nullable: false),
                    context_certainty = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    has_unverified_context = table.Column<bool>(type: "boolean", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjusted_projection", x => x.id);
                    table.ForeignKey(
                        name: "FK_adjusted_projection_baseline_projection_baseline_projection~",
                        column: x => x.baseline_projection_id,
                        principalTable: "baseline_projection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adjusted_projection_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "context_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    primary_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    affected_player_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expected_expiration = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    direction = table.Column<string>(type: "text", nullable: false),
                    magnitude = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    confidence = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    source_name = table.Column<string>(type: "text", nullable: false),
                    raw_text = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: false),
                    verification = table.Column<string>(type: "text", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_event", x => x.id);
                    table.ForeignKey(
                        name: "FK_context_event_nba_team_team_id",
                        column: x => x.team_id,
                        principalTable: "nba_team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_context_event_player_primary_player_id",
                        column: x => x.primary_player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fantasy_value",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fantasy_league_id = table.Column<Guid>(type: "uuid", nullable: false),
                    per_game = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    season_total = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    adjusted_projection_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fantasy_value", x => x.id);
                    table.ForeignKey(
                        name: "FK_fantasy_value_adjusted_projection_adjusted_projection_id",
                        column: x => x.adjusted_projection_id,
                        principalTable: "adjusted_projection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fantasy_value_fantasy_league_fantasy_league_id",
                        column: x => x.fantasy_league_id,
                        principalTable: "fantasy_league",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fantasy_value_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_context_impact",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    context_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minutes_delta = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    usage_delta = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    assist_share_delta = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    rebound_share_delta = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    shot_volume_delta = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    role_risk_delta = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    projection_confidence_delta = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    is_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    override_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    overridden_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_context_impact", x => x.id);
                    table.ForeignKey(
                        name: "FK_player_context_impact_context_event_context_event_id",
                        column: x => x.context_event_id,
                        principalTable: "context_event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_context_impact_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adjusted_projection_baseline_projection_id",
                table: "adjusted_projection",
                column: "baseline_projection_id");

            migrationBuilder.CreateIndex(
                name: "ix_adjusted_projection_player_computed_at",
                table: "adjusted_projection",
                columns: new[] { "player_id", "computed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_context_event_effective_expiration",
                table: "context_event",
                columns: new[] { "effective_from", "expected_expiration" });

            migrationBuilder.CreateIndex(
                name: "IX_context_event_primary_player_id",
                table: "context_event",
                column: "primary_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_context_event_team_id",
                table: "context_event",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_fantasy_value_adjusted_projection_id",
                table: "fantasy_value",
                column: "adjusted_projection_id");

            migrationBuilder.CreateIndex(
                name: "IX_fantasy_value_fantasy_league_id",
                table: "fantasy_value",
                column: "fantasy_league_id");

            migrationBuilder.CreateIndex(
                name: "ix_fantasy_value_player_league_adjusted",
                table: "fantasy_value",
                columns: new[] { "player_id", "fantasy_league_id", "adjusted_projection_id" });

            migrationBuilder.CreateIndex(
                name: "IX_player_context_impact_player_id",
                table: "player_context_impact",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ux_context_impact_event_player",
                table: "player_context_impact",
                columns: new[] { "context_event_id", "player_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fantasy_value");

            migrationBuilder.DropTable(
                name: "player_context_impact");

            migrationBuilder.DropTable(
                name: "adjusted_projection");

            migrationBuilder.DropTable(
                name: "context_event");
        }
    }
}

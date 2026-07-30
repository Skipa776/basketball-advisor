using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityAndNullableOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "scoring_rule",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "roster_slot",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "recommendation_evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "recommendation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "player_context_impact",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "fantasy_value",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "fantasy_league",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "draft_session",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "draft_pick",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "context_event",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "adjusted_projection",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fantasy_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    is_instance_owner = table.Column<bool>(type: "boolean", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fantasy_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_claim", x => x.id);
                    table.ForeignKey(
                        name: "FK_identity_user_claim_fantasy_user_user_id",
                        column: x => x.user_id,
                        principalTable: "fantasy_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_login",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_login", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_identity_user_login_fantasy_user_user_id",
                        column: x => x.user_id,
                        principalTable: "fantasy_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_token",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_token", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "FK_identity_user_token_fantasy_user_user_id",
                        column: x => x.user_id,
                        principalTable: "fantasy_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_role_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_role_claim", x => x.id);
                    table.ForeignKey(
                        name: "FK_identity_role_claim_identity_role_role_id",
                        column: x => x.role_id,
                        principalTable: "identity_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_user_role",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_user_role", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_identity_user_role_fantasy_user_user_id",
                        column: x => x.user_id,
                        principalTable: "fantasy_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_identity_user_role_identity_role_role_id",
                        column: x => x.role_id,
                        principalTable: "identity_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scoring_rule_owner_id",
                table: "scoring_rule",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_roster_slot_owner_id",
                table: "roster_slot",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_evidence_owner_id",
                table: "recommendation_evidence",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_owner_id",
                table: "recommendation",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_player_context_impact_owner_id",
                table: "player_context_impact",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_fantasy_value_owner_id",
                table: "fantasy_value",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_fantasy_league_owner_id",
                table: "fantasy_league",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_draft_session_owner_id",
                table: "draft_session",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_draft_pick_owner_id",
                table: "draft_pick",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_context_event_owner_id",
                table: "context_event",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_adjusted_projection_owner_id",
                table: "adjusted_projection",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "fantasy_user",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "IX_fantasy_user_is_instance_owner",
                table: "fantasy_user",
                column: "is_instance_owner",
                unique: true,
                filter: "\"is_instance_owner\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "fantasy_user",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "identity_role",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_role_claim_role_id",
                table: "identity_role_claim",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_claim_user_id",
                table: "identity_user_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_login_user_id",
                table: "identity_user_login",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_identity_user_role_role_id",
                table: "identity_user_role",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_adjusted_projection_fantasy_user_owner_id",
                table: "adjusted_projection",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_context_event_fantasy_user_owner_id",
                table: "context_event",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_draft_pick_fantasy_user_owner_id",
                table: "draft_pick",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_draft_session_fantasy_user_owner_id",
                table: "draft_session",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_fantasy_league_fantasy_user_owner_id",
                table: "fantasy_league",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_fantasy_value_fantasy_user_owner_id",
                table: "fantasy_value",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_player_context_impact_fantasy_user_owner_id",
                table: "player_context_impact",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recommendation_fantasy_user_owner_id",
                table: "recommendation",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recommendation_evidence_fantasy_user_owner_id",
                table: "recommendation_evidence",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_roster_slot_fantasy_user_owner_id",
                table: "roster_slot",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_scoring_rule_fantasy_user_owner_id",
                table: "scoring_rule",
                column: "owner_id",
                principalTable: "fantasy_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_adjusted_projection_fantasy_user_owner_id",
                table: "adjusted_projection");

            migrationBuilder.DropForeignKey(
                name: "FK_context_event_fantasy_user_owner_id",
                table: "context_event");

            migrationBuilder.DropForeignKey(
                name: "FK_draft_pick_fantasy_user_owner_id",
                table: "draft_pick");

            migrationBuilder.DropForeignKey(
                name: "FK_draft_session_fantasy_user_owner_id",
                table: "draft_session");

            migrationBuilder.DropForeignKey(
                name: "FK_fantasy_league_fantasy_user_owner_id",
                table: "fantasy_league");

            migrationBuilder.DropForeignKey(
                name: "FK_fantasy_value_fantasy_user_owner_id",
                table: "fantasy_value");

            migrationBuilder.DropForeignKey(
                name: "FK_player_context_impact_fantasy_user_owner_id",
                table: "player_context_impact");

            migrationBuilder.DropForeignKey(
                name: "FK_recommendation_fantasy_user_owner_id",
                table: "recommendation");

            migrationBuilder.DropForeignKey(
                name: "FK_recommendation_evidence_fantasy_user_owner_id",
                table: "recommendation_evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_roster_slot_fantasy_user_owner_id",
                table: "roster_slot");

            migrationBuilder.DropForeignKey(
                name: "FK_scoring_rule_fantasy_user_owner_id",
                table: "scoring_rule");

            migrationBuilder.DropTable(
                name: "identity_role_claim");

            migrationBuilder.DropTable(
                name: "identity_user_claim");

            migrationBuilder.DropTable(
                name: "identity_user_login");

            migrationBuilder.DropTable(
                name: "identity_user_role");

            migrationBuilder.DropTable(
                name: "identity_user_token");

            migrationBuilder.DropTable(
                name: "identity_role");

            migrationBuilder.DropTable(
                name: "fantasy_user");

            migrationBuilder.DropIndex(
                name: "IX_scoring_rule_owner_id",
                table: "scoring_rule");

            migrationBuilder.DropIndex(
                name: "IX_roster_slot_owner_id",
                table: "roster_slot");

            migrationBuilder.DropIndex(
                name: "IX_recommendation_evidence_owner_id",
                table: "recommendation_evidence");

            migrationBuilder.DropIndex(
                name: "IX_recommendation_owner_id",
                table: "recommendation");

            migrationBuilder.DropIndex(
                name: "IX_player_context_impact_owner_id",
                table: "player_context_impact");

            migrationBuilder.DropIndex(
                name: "IX_fantasy_value_owner_id",
                table: "fantasy_value");

            migrationBuilder.DropIndex(
                name: "IX_fantasy_league_owner_id",
                table: "fantasy_league");

            migrationBuilder.DropIndex(
                name: "IX_draft_session_owner_id",
                table: "draft_session");

            migrationBuilder.DropIndex(
                name: "IX_draft_pick_owner_id",
                table: "draft_pick");

            migrationBuilder.DropIndex(
                name: "IX_context_event_owner_id",
                table: "context_event");

            migrationBuilder.DropIndex(
                name: "IX_adjusted_projection_owner_id",
                table: "adjusted_projection");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "scoring_rule");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "roster_slot");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "recommendation_evidence");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "recommendation");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "player_context_impact");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "fantasy_value");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "fantasy_league");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "draft_session");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "draft_pick");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "context_event");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "adjusted_projection");
        }
    }
}

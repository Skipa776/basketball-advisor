using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_external_player_identity_player_id",
                table: "external_player_identity");

            migrationBuilder.CreateTable(
                name: "pending_identity_match",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    candidate_player_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_identity_match", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_external_player_identity_player_provider",
                table: "external_player_identity",
                columns: new[] { "player_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pending_identity_match_provider_external_id",
                table: "pending_identity_match",
                columns: new[] { "provider", "external_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_identity_match");

            migrationBuilder.DropIndex(
                name: "ux_external_player_identity_player_provider",
                table: "external_player_identity");

            migrationBuilder.CreateIndex(
                name: "IX_external_player_identity_player_id",
                table: "external_player_identity",
                column: "player_id");
        }
    }
}

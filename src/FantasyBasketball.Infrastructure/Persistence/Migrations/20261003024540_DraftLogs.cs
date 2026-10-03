using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DraftLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "draft_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    draft_id = table.Column<string>(type: "text", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    team_count = table.Column<int>(type: "integer", nullable: false),
                    rounds = table.Column<int>(type: "integer", nullable: false),
                    scoring_type = table.Column<string>(type: "text", nullable: true),
                    slots = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_draft_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "draft_log_pick",
                columns: table => new
                {
                    draft_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pick_number = table.Column<int>(type: "integer", nullable: false),
                    round = table.Column<int>(type: "integer", nullable: false),
                    draft_slot = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<string>(type: "text", nullable: false),
                    player_name = table.Column<string>(type: "text", nullable: false),
                    positions = table.Column<string[]>(type: "text[]", nullable: false),
                    picked_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_draft_log_pick", x => new { x.draft_log_id, x.pick_number });
                    table.ForeignKey(
                        name: "FK_draft_log_pick_draft_log_draft_log_id",
                        column: x => x.draft_log_id,
                        principalTable: "draft_log",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_draft_log_source_draft",
                table: "draft_log",
                columns: new[] { "source", "draft_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "draft_log_pick");

            migrationBuilder.DropTable(
                name: "draft_log");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlayerAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_availability",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    body_part = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    parser_version = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    raw_record_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_availability", x => x.id);
                    table.ForeignKey(
                        name: "FK_player_availability_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_availability_player_id_source",
                table: "player_availability",
                columns: new[] { "player_id", "source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_availability");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdpEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adp_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    average_draft_position = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    standard_deviation = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_adp_entry", x => x.id);
                    table.ForeignKey(
                        name: "FK_adp_entry_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_adp_entry_player_fetched_at",
                table: "adp_entry",
                columns: new[] { "player_id", "fetched_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adp_entry");
        }
    }
}

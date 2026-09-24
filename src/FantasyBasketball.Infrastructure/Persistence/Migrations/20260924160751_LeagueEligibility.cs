using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeagueEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "league_player_eligibility",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fantasy_league_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    positions = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_player_eligibility", x => x.id);
                    table.ForeignKey(
                        name: "FK_league_player_eligibility_fantasy_league_fantasy_league_id",
                        column: x => x.fantasy_league_id,
                        principalTable: "fantasy_league",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_league_player_eligibility_fantasy_user_owner_id",
                        column: x => x.owner_id,
                        principalTable: "fantasy_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_league_player_eligibility_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_league_player_eligibility_fantasy_league_id_player_id",
                table: "league_player_eligibility",
                columns: new[] { "fantasy_league_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_player_eligibility_owner_id",
                table: "league_player_eligibility",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_league_player_eligibility_player_id",
                table: "league_player_eligibility",
                column: "player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "league_player_eligibility");
        }
    }
}

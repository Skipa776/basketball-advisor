using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeagueRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "league_team",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fantasy_league_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_users_team = table.Column<bool>(type: "boolean", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_team", x => x.id);
                    table.ForeignKey(
                        name: "FK_league_team_fantasy_league_fantasy_league_id",
                        column: x => x.fantasy_league_id,
                        principalTable: "fantasy_league",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_league_team_fantasy_user_owner_id",
                        column: x => x.owner_id,
                        principalTable: "fantasy_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "league_roster_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    league_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_roster_entry", x => x.id);
                    table.ForeignKey(
                        name: "FK_league_roster_entry_fantasy_user_owner_id",
                        column: x => x.owner_id,
                        principalTable: "fantasy_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_league_roster_entry_league_team_league_team_id",
                        column: x => x.league_team_id,
                        principalTable: "league_team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_league_roster_entry_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_league_roster_entry_league_team_id_player_id",
                table: "league_roster_entry",
                columns: new[] { "league_team_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_roster_entry_owner_id",
                table: "league_roster_entry",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_league_roster_entry_player_id",
                table: "league_roster_entry",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_league_team_fantasy_league_id_ordinal",
                table: "league_team",
                columns: new[] { "fantasy_league_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_team_owner_id",
                table: "league_team",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "league_roster_entry");

            migrationBuilder.DropTable(
                name: "league_team");
        }
    }
}

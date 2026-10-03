using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectionDistributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "per_game_sd",
                table: "fantasy_value",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "projection_distribution",
                columns: table => new
                {
                    baseline_projection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    per_game_mean = table.Column<string>(type: "jsonb", nullable: false),
                    covariance = table.Column<string>(type: "jsonb", nullable: false),
                    games_alpha = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    games_beta = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    season_games = table.Column<int>(type: "integer", nullable: false),
                    model_version = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_distribution", x => x.baseline_projection_id);
                    table.ForeignKey(
                        name: "FK_projection_distribution_baseline_projection_baseline_projec~",
                        column: x => x.baseline_projection_id,
                        principalTable: "baseline_projection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projection_distribution");

            migrationBuilder.DropColumn(
                name: "per_game_sd",
                table: "fantasy_value");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectionPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fantasy_value_fantasy_league_id",
                table: "fantasy_value");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "computed_at",
                table: "fantasy_value",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "publication_id",
                table: "fantasy_value",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scoring_profile",
                table: "fantasy_value",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "observed_stats_id",
                table: "baseline_projection",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_fantasy_value_fantasy_league_id_player_id_computed_at",
                table: "fantasy_value",
                columns: new[] { "fantasy_league_id", "player_id", "computed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_baseline_projection_observed_stats_id",
                table: "baseline_projection",
                column: "observed_stats_id");

            migrationBuilder.AddForeignKey(
                name: "FK_baseline_projection_observed_stats_observed_stats_id",
                table: "baseline_projection",
                column: "observed_stats_id",
                principalTable: "observed_stats",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_baseline_projection_observed_stats_observed_stats_id",
                table: "baseline_projection");

            migrationBuilder.DropIndex(
                name: "IX_fantasy_value_fantasy_league_id_player_id_computed_at",
                table: "fantasy_value");

            migrationBuilder.DropIndex(
                name: "IX_baseline_projection_observed_stats_id",
                table: "baseline_projection");

            migrationBuilder.DropColumn(
                name: "computed_at",
                table: "fantasy_value");

            migrationBuilder.DropColumn(
                name: "publication_id",
                table: "fantasy_value");

            migrationBuilder.DropColumn(
                name: "scoring_profile",
                table: "fantasy_value");

            migrationBuilder.DropColumn(
                name: "observed_stats_id",
                table: "baseline_projection");

            migrationBuilder.CreateIndex(
                name: "IX_fantasy_value_fantasy_league_id",
                table: "fantasy_value",
                column: "fantasy_league_id");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyAcquisitionLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "weekly_acquisition_limit",
                table: "fantasy_league",
                type: "integer",
                nullable: false,
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "weekly_acquisition_limit",
                table: "fantasy_league");
        }
    }
}

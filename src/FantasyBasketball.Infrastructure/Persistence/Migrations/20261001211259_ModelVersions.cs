using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModelVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_name = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    fitted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    train_season_end_years = table.Column<int[]>(type: "integer[]", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    metrics = table.Column<string>(type: "jsonb", nullable: false),
                    card_markdown = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_version", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_model_version_active_per_model",
                table: "model_version",
                column: "model_name",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "ux_model_version_name_version",
                table: "model_version",
                columns: new[] { "model_name", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_version");
        }
    }
}

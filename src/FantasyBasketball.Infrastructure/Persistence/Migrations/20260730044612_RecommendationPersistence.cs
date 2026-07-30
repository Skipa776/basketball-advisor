using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FantasyBasketball.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recommendation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    subject_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    score = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    confidence = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendation_player_subject_player_id",
                        column: x => x.subject_player_id,
                        principalTable: "player",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    polarity = table.Column<string>(type: "text", nullable: false),
                    statement = table.Column<string>(type: "text", nullable: false),
                    magnitude = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendation_evidence_recommendation_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_subject_player_id",
                table: "recommendation",
                column: "subject_player_id");

            migrationBuilder.CreateIndex(
                name: "ux_recommendation_evidence_ordinal",
                table: "recommendation_evidence",
                columns: new[] { "recommendation_id", "ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_evidence");

            migrationBuilder.DropTable(
                name: "recommendation");
        }
    }
}

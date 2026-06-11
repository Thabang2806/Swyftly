using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiProductModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_moderation_results",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NeedsAdminReview = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    detected_terms_json = table.Column<string>(type: "jsonb", nullable: false),
                    missing_fields_json = table.Column<string>(type: "jsonb", nullable: false),
                    flags_json = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_moderation_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_moderation_results_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_moderation_results_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_moderation_results_CreatedAtUtc",
                schema: "mabuntle",
                table: "ai_moderation_results",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ai_moderation_results_NeedsAdminReview",
                schema: "mabuntle",
                table: "ai_moderation_results",
                column: "NeedsAdminReview");

            migrationBuilder.CreateIndex(
                name: "IX_ai_moderation_results_ProductId",
                schema: "mabuntle",
                table: "ai_moderation_results",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_moderation_results_SellerId",
                schema: "mabuntle",
                table: "ai_moderation_results",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_moderation_results",
                schema: "mabuntle");
        }
    }
}

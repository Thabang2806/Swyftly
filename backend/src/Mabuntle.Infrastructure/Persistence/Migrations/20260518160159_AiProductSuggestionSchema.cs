using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiProductSuggestionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_product_suggestions",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    input_image_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    SuggestedTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SuggestedShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuggestedFullDescription = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    SuggestedCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuggestedCategoryPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    suggested_attributes_json = table.Column<string>(type: "jsonb", nullable: false),
                    suggested_tags_json = table.Column<string>(type: "jsonb", nullable: false),
                    missing_fields_json = table.Column<string>(type: "jsonb", nullable: false),
                    risk_flags_json = table.Column<string>(type: "jsonb", nullable: false),
                    QualityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ModelUsed = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_product_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_product_suggestions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_product_suggestions_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_prompt_versions",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PromptTemplate = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_prompt_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage_logs",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelUsed = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputTokenEstimate = table.Column<int>(type: "integer", nullable: true),
                    OutputTokenEstimate = table.Column<int>(type: "integer", nullable: true),
                    CostEstimate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_usage_logs_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_suggestion_field_audits",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ai_value = table.Column<string>(type: "jsonb", nullable: true),
                    seller_final_value = table.Column<string>(type: "jsonb", nullable: true),
                    WasAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    WasEdited = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_suggestion_field_audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_suggestion_field_audits_ai_product_suggestions_Suggestio~",
                        column: x => x.SuggestionId,
                        principalSchema: "mabuntle",
                        principalTable: "ai_product_suggestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_product_suggestions_CreatedAtUtc",
                schema: "mabuntle",
                table: "ai_product_suggestions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ai_product_suggestions_ProductId",
                schema: "mabuntle",
                table: "ai_product_suggestions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_product_suggestions_SellerId",
                schema: "mabuntle",
                table: "ai_product_suggestions",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_product_suggestions_Status",
                schema: "mabuntle",
                table: "ai_product_suggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ai_prompt_versions_FeatureName_Version",
                schema: "mabuntle",
                table: "ai_prompt_versions",
                columns: new[] { "FeatureName", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_suggestion_field_audits_SuggestionId",
                schema: "mabuntle",
                table: "ai_suggestion_field_audits",
                column: "SuggestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_CreatedAtUtc",
                schema: "mabuntle",
                table: "ai_usage_logs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_FeatureName",
                schema: "mabuntle",
                table: "ai_usage_logs",
                column: "FeatureName");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_SellerId",
                schema: "mabuntle",
                table: "ai_usage_logs",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_UserId",
                schema: "mabuntle",
                table: "ai_usage_logs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_prompt_versions",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ai_suggestion_field_audits",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ai_usage_logs",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ai_product_suggestions",
                schema: "mabuntle");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerAiDiscoveryOutcomeAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buyer_growth_outcomes",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceTool = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    CartId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceBand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttributedFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttributionWindowMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_growth_outcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buyer_growth_outcomes_buyer_growth_events_SourceEventId",
                        column: x => x.SourceEventId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_growth_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_buyer_growth_outcomes_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_buyer_growth_outcomes_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "mabuntle",
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_buyer_growth_outcomes_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_buyer_growth_outcomes_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_outcomes_BuyerId_OutcomeType_OccurredAtUtc",
                schema: "mabuntle",
                table: "buyer_growth_outcomes",
                columns: new[] { "BuyerId", "OutcomeType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_outcomes_BuyerId_OutcomeType_SourceTool_Produc~",
                schema: "mabuntle",
                table: "buyer_growth_outcomes",
                columns: new[] { "BuyerId", "OutcomeType", "SourceTool", "ProductId", "CartId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_outcomes_CartId_OutcomeType",
                schema: "mabuntle",
                table: "buyer_growth_outcomes",
                columns: new[] { "CartId", "OutcomeType" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_outcomes_OrderId_OutcomeType",
                schema: "mabuntle",
                table: "buyer_growth_outcomes",
                columns: new[] { "OrderId", "OutcomeType" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_outcomes_OutcomeType_SourceTool_OccurredAtUtc",
                schema: "mabuntle",
                table: "buyer_growth_outcomes",
                columns: new[] { "OutcomeType", "SourceTool", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_outcomes_ProductId_OutcomeType_OccurredAtUtc",
                schema: "mabuntle",
                table: "buyer_growth_outcomes",
                columns: new[] { "ProductId", "OutcomeType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_outcomes_SourceEventId_OutcomeType",
                schema: "mabuntle",
                table: "buyer_growth_outcomes",
                columns: new[] { "SourceEventId", "OutcomeType" },
                unique: true,
                filter: "\"SourceEventId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buyer_growth_outcomes",
                schema: "mabuntle");
        }
    }
}

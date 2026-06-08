using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerGrowthDiscoveryTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buyer_growth_events",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceTool = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultCount = table.Column<int>(type: "integer", nullable: true),
                    ConfidenceBand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Colour = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Material = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceRoute = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FeedbackReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_growth_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buyer_growth_events_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_buyer_growth_events_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_events_BuyerId_EventType_OccurredAtUtc",
                schema: "mabuntle",
                table: "buyer_growth_events",
                columns: new[] { "BuyerId", "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_events_EventType_SourceTool_OccurredAtUtc",
                schema: "mabuntle",
                table: "buyer_growth_events",
                columns: new[] { "EventType", "SourceTool", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_growth_events_ProductId_EventType_OccurredAtUtc",
                schema: "mabuntle",
                table: "buyer_growth_events",
                columns: new[] { "ProductId", "EventType", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buyer_growth_events",
                schema: "mabuntle");
        }
    }
}

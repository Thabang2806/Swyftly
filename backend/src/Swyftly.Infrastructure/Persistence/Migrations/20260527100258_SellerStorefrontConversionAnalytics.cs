using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerStorefrontConversionAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seller_funnel_events",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    CartId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: true),
                    HashedAnonymousVisitorId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceRoute = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_funnel_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_funnel_events_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_seller_funnel_events_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "mabuntle",
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_seller_funnel_events_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_seller_funnel_events_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_seller_funnel_events_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_BuyerId",
                schema: "mabuntle",
                table: "seller_funnel_events",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_CartId",
                schema: "mabuntle",
                table: "seller_funnel_events",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_OrderId",
                schema: "mabuntle",
                table: "seller_funnel_events",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_ProductId",
                schema: "mabuntle",
                table: "seller_funnel_events",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_SellerId_EventType_IdempotencyKey",
                schema: "mabuntle",
                table: "seller_funnel_events",
                columns: new[] { "SellerId", "EventType", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_SellerId_EventType_OccurredAtUtc",
                schema: "mabuntle",
                table: "seller_funnel_events",
                columns: new[] { "SellerId", "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_SellerId_ProductId_EventType_OccurredA~",
                schema: "mabuntle",
                table: "seller_funnel_events",
                columns: new[] { "SellerId", "ProductId", "EventType", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_funnel_events",
                schema: "mabuntle");
        }
    }
}

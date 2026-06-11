using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReturnRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "return_requests",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SellerRespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SellerRespondedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellerResponseReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DisputedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisputedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisputeReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_return_requests_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_requests_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_requests_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_items",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsOpenedOrUnsealed = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_return_items_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "mabuntle",
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_items_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "mabuntle",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_items_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_items_return_requests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalSchema: "mabuntle",
                        principalTable: "return_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_messages",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_return_messages_return_requests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalSchema: "mabuntle",
                        principalTable: "return_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_return_items_OrderItemId",
                schema: "mabuntle",
                table: "return_items",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_ProductId",
                schema: "mabuntle",
                table: "return_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_ProductVariantId",
                schema: "mabuntle",
                table: "return_items",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_ReturnRequestId",
                schema: "mabuntle",
                table: "return_items",
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_return_messages_CreatedAtUtc",
                schema: "mabuntle",
                table: "return_messages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_return_messages_ReturnRequestId",
                schema: "mabuntle",
                table: "return_messages",
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_return_messages_SenderUserId",
                schema: "mabuntle",
                table: "return_messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_BuyerId",
                schema: "mabuntle",
                table: "return_requests",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_OrderId",
                schema: "mabuntle",
                table: "return_requests",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_Reason",
                schema: "mabuntle",
                table: "return_requests",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_RequestedAtUtc",
                schema: "mabuntle",
                table: "return_requests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_SellerId",
                schema: "mabuntle",
                table: "return_requests",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_Status",
                schema: "mabuntle",
                table: "return_requests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "return_items",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "return_messages",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "return_requests",
                schema: "mabuntle");
        }
    }
}

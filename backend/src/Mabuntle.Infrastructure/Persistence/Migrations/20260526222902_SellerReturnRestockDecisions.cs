using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerReturnRestockDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "return_restock_decisions",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityRestocked = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_restock_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_return_restock_decisions_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "mabuntle",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_restock_decisions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_return_restock_decisions_return_items_ReturnItemId",
                        column: x => x.ReturnItemId,
                        principalSchema: "mabuntle",
                        principalTable: "return_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_return_restock_decisions_return_requests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalSchema: "mabuntle",
                        principalTable: "return_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_return_restock_decisions_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_return_restock_decisions_ProductId",
                schema: "mabuntle",
                table: "return_restock_decisions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_return_restock_decisions_ProductVariantId",
                schema: "mabuntle",
                table: "return_restock_decisions",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_return_restock_decisions_ReturnItemId",
                schema: "mabuntle",
                table: "return_restock_decisions",
                column: "ReturnItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_return_restock_decisions_ReturnRequestId",
                schema: "mabuntle",
                table: "return_restock_decisions",
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_return_restock_decisions_SellerId",
                schema: "mabuntle",
                table: "return_restock_decisions",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "return_restock_decisions",
                schema: "mabuntle");
        }
    }
}

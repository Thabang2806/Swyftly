using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerInventoryMovementHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_movements",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StockQuantityBefore = table.Column<int>(type: "integer", nullable: false),
                    StockQuantityAfter = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantityBefore = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantityAfter = table.Column<int>(type: "integer", nullable: false),
                    QuantityDelta = table.Column<int>(type: "integer", nullable: false),
                    StatusBefore = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StatusAfter = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_product_variants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalSchema: "mabuntle",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_movements_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_BatchReference",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "BatchReference");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_MovementType",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_ProductId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_ProductVariantId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_SellerId_OccurredAtUtc",
                schema: "mabuntle",
                table: "inventory_movements",
                columns: new[] { "SellerId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_movements",
                schema: "mabuntle");
        }
    }
}

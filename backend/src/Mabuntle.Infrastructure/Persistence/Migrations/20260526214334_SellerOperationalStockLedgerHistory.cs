using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerOperationalStockLedgerHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CartId",
                schema: "mabuntle",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                schema: "mabuntle",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                schema: "mabuntle",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundId",
                schema: "mabuntle",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                schema: "mabuntle",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnRequestId",
                schema: "mabuntle",
                table: "inventory_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_CartId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_OrderId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_PaymentId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_RefundId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_ReservationId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_ReturnRequestId",
                schema: "mabuntle",
                table: "inventory_movements",
                column: "ReturnRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_CartId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_OrderId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_PaymentId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_RefundId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_ReservationId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "IX_inventory_movements_ReturnRequestId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "CartId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "RefundId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                schema: "mabuntle",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "ReturnRequestId",
                schema: "mabuntle",
                table: "inventory_movements");
        }
    }
}

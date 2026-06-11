using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancePaymentRefundIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_OrderId",
                schema: "mabuntle",
                table: "payments");

            migrationBuilder.AddColumn<int>(
                name: "ConcurrencyVersion",
                schema: "mabuntle",
                table: "refunds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrderId",
                schema: "mabuntle",
                table: "payments",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" NOT IN ('Failed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_OrderId",
                schema: "mabuntle",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ConcurrencyVersion",
                schema: "mabuntle",
                table: "refunds");

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrderId",
                schema: "mabuntle",
                table: "payments",
                column: "OrderId");
        }
    }
}

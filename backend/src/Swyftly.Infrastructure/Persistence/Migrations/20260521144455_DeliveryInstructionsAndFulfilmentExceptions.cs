using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryInstructionsAndFulfilmentExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryInstructions",
                schema: "mabuntle",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryInstructions",
                schema: "mabuntle",
                table: "buyer_delivery_addresses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryInstructions",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryInstructions",
                schema: "mabuntle",
                table: "buyer_delivery_addresses");
        }
    }
}

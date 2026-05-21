using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerDeliveryAddressesAndOrderDeliverySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddressLine1",
                schema: "swyftly",
                table: "orders",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddressLine2",
                schema: "swyftly",
                table: "orders",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                schema: "swyftly",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCountryCode",
                schema: "swyftly",
                table: "orders",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPhoneNumber",
                schema: "swyftly",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPostalCode",
                schema: "swyftly",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryProvince",
                schema: "swyftly",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryRecipientName",
                schema: "swyftly",
                table: "orders",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliverySuburb",
                schema: "swyftly",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "buyer_delivery_addresses",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Suburb = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Province = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_delivery_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buyer_delivery_addresses_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "swyftly",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_delivery_addresses_BuyerId",
                schema: "swyftly",
                table: "buyer_delivery_addresses",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_buyer_delivery_addresses_BuyerId_IsDefault",
                schema: "swyftly",
                table: "buyer_delivery_addresses",
                columns: new[] { "BuyerId", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buyer_delivery_addresses",
                schema: "swyftly");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressLine1",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressLine2",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryCountryCode",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPhoneNumber",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPostalCode",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryProvince",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryRecipientName",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliverySuburb",
                schema: "swyftly",
                table: "orders");
        }
    }
}

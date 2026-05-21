using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerDeliveryMethodsAndShippingRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryEstimatedMaxDays",
                schema: "swyftly",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryEstimatedMinDays",
                schema: "swyftly",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryMethodId",
                schema: "swyftly",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMethodName",
                schema: "swyftly",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMethodType",
                schema: "swyftly",
                table: "orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seller_delivery_methods",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MethodType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Province = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FreeShippingThreshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EstimatedMinDays = table.Column<int>(type: "integer", nullable: false),
                    EstimatedMaxDays = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_delivery_methods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_delivery_methods_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "swyftly",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_delivery_methods_SellerId",
                schema: "swyftly",
                table: "seller_delivery_methods",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_delivery_methods_SellerId_CountryCode_Province",
                schema: "swyftly",
                table: "seller_delivery_methods",
                columns: new[] { "SellerId", "CountryCode", "Province" });

            migrationBuilder.CreateIndex(
                name: "IX_seller_delivery_methods_SellerId_IsActive_DisplayOrder",
                schema: "swyftly",
                table: "seller_delivery_methods",
                columns: new[] { "SellerId", "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_delivery_methods",
                schema: "swyftly");

            migrationBuilder.DropColumn(
                name: "DeliveryEstimatedMaxDays",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryEstimatedMinDays",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryMethodId",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryMethodName",
                schema: "swyftly",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveryMethodType",
                schema: "swyftly",
                table: "orders");
        }
    }
}

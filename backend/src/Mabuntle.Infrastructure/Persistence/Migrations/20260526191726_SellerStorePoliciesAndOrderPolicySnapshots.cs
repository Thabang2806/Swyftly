using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerStorePoliciesAndOrderPolicySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyCareInstructions",
                schema: "mabuntle",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyExchangePolicy",
                schema: "mabuntle",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyFulfilmentPolicy",
                schema: "mabuntle",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyProductDisclaimer",
                schema: "mabuntle",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicyReturnPolicy",
                schema: "mabuntle",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SellerPolicyReturnWindowDays",
                schema: "mabuntle",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SellerPolicySnapshotAtUtc",
                schema: "mabuntle",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerPolicySupportPolicy",
                schema: "mabuntle",
                table: "orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seller_store_policies",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnWindowDays = table.Column<int>(type: "integer", nullable: true),
                    ReturnPolicy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExchangePolicy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FulfilmentPolicy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SupportPolicy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CareInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProductDisclaimer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_store_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_store_policies_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_store_policies_SellerId",
                schema: "mabuntle",
                table: "seller_store_policies",
                column: "SellerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_store_policies",
                schema: "mabuntle");

            migrationBuilder.DropColumn(
                name: "SellerPolicyCareInstructions",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyExchangePolicy",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyFulfilmentPolicy",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyProductDisclaimer",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyReturnPolicy",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicyReturnWindowDays",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicySnapshotAtUtc",
                schema: "mabuntle",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SellerPolicySupportPolicy",
                schema: "mabuntle",
                table: "orders");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerPayoutWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seller_payouts",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HeldAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HeldByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    HoldReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_payouts_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seller_payout_items",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerPayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_payout_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_payout_items_ledger_entries_LedgerEntryId",
                        column: x => x.LedgerEntryId,
                        principalSchema: "mabuntle",
                        principalTable: "ledger_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seller_payout_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seller_payout_items_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "mabuntle",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seller_payout_items_seller_payouts_SellerPayoutId",
                        column: x => x.SellerPayoutId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_items_LedgerEntryId",
                schema: "mabuntle",
                table: "seller_payout_items",
                column: "LedgerEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_items_OrderId",
                schema: "mabuntle",
                table: "seller_payout_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_items_PaymentId",
                schema: "mabuntle",
                table: "seller_payout_items",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_items_SellerPayoutId",
                schema: "mabuntle",
                table: "seller_payout_items",
                column: "SellerPayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payouts_CreatedAtUtc",
                schema: "mabuntle",
                table: "seller_payouts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payouts_SellerId",
                schema: "mabuntle",
                table: "seller_payouts",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payouts_Status",
                schema: "mabuntle",
                table: "seller_payouts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_payout_items",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "seller_payouts",
                schema: "mabuntle");
        }
    }
}

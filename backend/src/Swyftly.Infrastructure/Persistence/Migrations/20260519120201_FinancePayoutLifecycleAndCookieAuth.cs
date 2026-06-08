using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancePayoutLifecycleAndCookieAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvailabilityReason",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAtUtc",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvailableByUserId",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConcurrencyVersion",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedAtUtc",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeldFromStatus",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidOutAtUtc",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingAtUtc",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingByUserId",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingReason",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPayoutReference",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                schema: "mabuntle",
                table: "seller_payouts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustedAmount",
                schema: "mabuntle",
                table: "seller_payout_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RequestedByRole",
                schema: "mabuntle",
                table: "refunds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                schema: "mabuntle",
                table: "refunds",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE mabuntle.refunds
                SET "RequestedByRole" = 'System'
                WHERE "RequestedByRole" = '';
                """);

            migrationBuilder.Sql("""
                UPDATE mabuntle.refunds
                SET "RequestedByUserId" = '00000000-0000-0000-0000-000000000001'::uuid
                WHERE "RequestedByUserId" = '00000000-0000-0000-0000-000000000000'::uuid;
                """);

            migrationBuilder.CreateTable(
                name: "seller_payout_adjustments",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerPayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerPayoutItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AdjustmentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_payout_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_payout_adjustments_ledger_entries_RefundLedgerEntryId",
                        column: x => x.RefundLedgerEntryId,
                        principalSchema: "mabuntle",
                        principalTable: "ledger_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seller_payout_adjustments_refunds_RefundId",
                        column: x => x.RefundId,
                        principalSchema: "mabuntle",
                        principalTable: "refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seller_payout_adjustments_seller_payout_items_SellerPayoutI~",
                        column: x => x.SellerPayoutItemId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_payout_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_seller_payout_adjustments_seller_payouts_SellerPayoutId",
                        column: x => x.SellerPayoutId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_adjustments_CreatedAtUtc",
                schema: "mabuntle",
                table: "seller_payout_adjustments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_adjustments_RefundId",
                schema: "mabuntle",
                table: "seller_payout_adjustments",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_adjustments_RefundLedgerEntryId",
                schema: "mabuntle",
                table: "seller_payout_adjustments",
                column: "RefundLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_adjustments_SellerPayoutId",
                schema: "mabuntle",
                table: "seller_payout_adjustments",
                column: "SellerPayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_adjustments_SellerPayoutItemId",
                schema: "mabuntle",
                table: "seller_payout_adjustments",
                column: "SellerPayoutItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_payout_adjustments",
                schema: "mabuntle");

            migrationBuilder.DropColumn(
                name: "AvailabilityReason",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "AvailableAtUtc",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "AvailableByUserId",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "ConcurrencyVersion",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "HeldFromStatus",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "PaidOutAtUtc",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "ProcessingAtUtc",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "ProcessingByUserId",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "ProcessingReason",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "ProviderPayoutReference",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                schema: "mabuntle",
                table: "seller_payouts");

            migrationBuilder.DropColumn(
                name: "AdjustedAmount",
                schema: "mabuntle",
                table: "seller_payout_items");

            migrationBuilder.DropColumn(
                name: "RequestedByRole",
                schema: "mabuntle",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                schema: "mabuntle",
                table: "refunds");
        }
    }
}

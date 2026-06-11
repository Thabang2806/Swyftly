using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentsEventsAndLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commission_rules",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlatformCommissionRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PaymentProviderFeeRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PaymentProviderFixedFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commission_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seller_balances",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PendingBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HeldBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_balances_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_order_items_OrderItemId",
                        column: x => x.OrderItemId,
                        principalSchema: "mabuntle",
                        principalTable: "order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "mabuntle",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_events",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    raw_payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_events_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "mabuntle",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_commission_rules_IsActive",
                schema: "mabuntle",
                table: "commission_rules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_BuyerId",
                schema: "mabuntle",
                table: "ledger_entries",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_CreatedAtUtc",
                schema: "mabuntle",
                table: "ledger_entries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrderId",
                schema: "mabuntle",
                table: "ledger_entries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrderItemId",
                schema: "mabuntle",
                table: "ledger_entries",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PaymentId",
                schema: "mabuntle",
                table: "ledger_entries",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PaymentId_Type",
                schema: "mabuntle",
                table: "ledger_entries",
                columns: new[] { "PaymentId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_SellerId",
                schema: "mabuntle",
                table: "ledger_entries",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_events_PaymentId",
                schema: "mabuntle",
                table: "payment_events",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_events_Provider_ProviderEventId",
                schema: "mabuntle",
                table: "payment_events",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_events_ReceivedAtUtc",
                schema: "mabuntle",
                table: "payment_events",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_payments_BuyerId",
                schema: "mabuntle",
                table: "payments",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_CreatedAtUtc",
                schema: "mabuntle",
                table: "payments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrderId",
                schema: "mabuntle",
                table: "payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_ProviderReference",
                schema: "mabuntle",
                table: "payments",
                column: "ProviderReference");

            migrationBuilder.CreateIndex(
                name: "IX_payments_Status",
                schema: "mabuntle",
                table: "payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_seller_balances_SellerId_Currency",
                schema: "mabuntle",
                table: "seller_balances",
                columns: new[] { "SellerId", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commission_rules",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "payment_events",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "seller_balances",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "mabuntle");
        }
    }
}

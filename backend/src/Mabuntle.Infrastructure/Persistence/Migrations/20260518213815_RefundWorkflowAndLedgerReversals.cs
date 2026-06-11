using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefundWorkflowAndLedgerReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProviderRefundReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefundedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refunds_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "mabuntle",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_return_requests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalSchema: "mabuntle",
                        principalTable: "return_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refund_events",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refund_events_refunds_RefundId",
                        column: x => x.RefundId,
                        principalSchema: "mabuntle",
                        principalTable: "refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refund_events_CreatedAtUtc",
                schema: "mabuntle",
                table: "refund_events",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_refund_events_EventType",
                schema: "mabuntle",
                table: "refund_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_refund_events_RefundId",
                schema: "mabuntle",
                table: "refund_events",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_refund_events_Status",
                schema: "mabuntle",
                table: "refund_events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_BuyerId",
                schema: "mabuntle",
                table: "refunds",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_OrderId",
                schema: "mabuntle",
                table: "refunds",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_PaymentId",
                schema: "mabuntle",
                table: "refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_RequestedAtUtc",
                schema: "mabuntle",
                table: "refunds",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_ReturnRequestId",
                schema: "mabuntle",
                table: "refunds",
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_SellerId",
                schema: "mabuntle",
                table: "refunds",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_Status",
                schema: "mabuntle",
                table: "refunds",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refund_events",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "refunds",
                schema: "mabuntle");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentReconciliationReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_reconciliation_reviews",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ObservedProviderStatus = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ObservedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextReviewAfterUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_reconciliation_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_reconciliation_reviews_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "mabuntle",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_reconciliation_reviews_NextReviewAfterUtc",
                schema: "mabuntle",
                table: "payment_reconciliation_reviews",
                column: "NextReviewAfterUtc");

            migrationBuilder.CreateIndex(
                name: "IX_payment_reconciliation_reviews_Outcome",
                schema: "mabuntle",
                table: "payment_reconciliation_reviews",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_payment_reconciliation_reviews_PaymentId_ReviewedAtUtc",
                schema: "mabuntle",
                table: "payment_reconciliation_reviews",
                columns: new[] { "PaymentId", "ReviewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_reconciliation_reviews_Provider_ProviderReference",
                schema: "mabuntle",
                table: "payment_reconciliation_reviews",
                columns: new[] { "Provider", "ProviderReference" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_reconciliation_reviews",
                schema: "mabuntle");
        }
    }
}

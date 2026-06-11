using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DisputeWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "disputes",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_disputes_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_disputes_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_disputes_return_requests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalSchema: "mabuntle",
                        principalTable: "return_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_disputes_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispute_evidence",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisputeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StorageReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispute_evidence_disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalSchema: "mabuntle",
                        principalTable: "disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispute_messages",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisputeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispute_messages_disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalSchema: "mabuntle",
                        principalTable: "disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispute_evidence_CreatedAtUtc",
                schema: "mabuntle",
                table: "dispute_evidence",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_evidence_DisputeId",
                schema: "mabuntle",
                table: "dispute_evidence",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_evidence_EvidenceType",
                schema: "mabuntle",
                table: "dispute_evidence",
                column: "EvidenceType");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_evidence_SubmittedByUserId",
                schema: "mabuntle",
                table: "dispute_evidence",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_messages_CreatedAtUtc",
                schema: "mabuntle",
                table: "dispute_messages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_messages_DisputeId",
                schema: "mabuntle",
                table: "dispute_messages",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_messages_SenderUserId",
                schema: "mabuntle",
                table: "dispute_messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_BuyerId",
                schema: "mabuntle",
                table: "disputes",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_OpenedAtUtc",
                schema: "mabuntle",
                table: "disputes",
                column: "OpenedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_OrderId",
                schema: "mabuntle",
                table: "disputes",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_ReturnRequestId",
                schema: "mabuntle",
                table: "disputes",
                column: "ReturnRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_SellerId",
                schema: "mabuntle",
                table: "disputes",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_Status",
                schema: "mabuntle",
                table: "disputes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispute_evidence",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "dispute_messages",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "disputes",
                schema: "mabuntle");
        }
    }
}

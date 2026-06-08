using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportTicketSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_tickets",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    LinkedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedSellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedSupportUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_support_tickets_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_orders_LinkedOrderId",
                        column: x => x.LinkedOrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_payments_LinkedPaymentId",
                        column: x => x.LinkedPaymentId,
                        principalSchema: "mabuntle",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_products_LinkedProductId",
                        column: x => x.LinkedProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_seller_profiles_LinkedSellerId",
                        column: x => x.LinkedSellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_support_tickets_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "support_messages",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_support_messages_support_tickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalSchema: "mabuntle",
                        principalTable: "support_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_CreatedAtUtc",
                schema: "mabuntle",
                table: "support_messages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_IsInternal",
                schema: "mabuntle",
                table: "support_messages",
                column: "IsInternal");

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_SenderUserId",
                schema: "mabuntle",
                table: "support_messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_support_messages_SupportTicketId",
                schema: "mabuntle",
                table: "support_messages",
                column: "SupportTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_BuyerId",
                schema: "mabuntle",
                table: "support_tickets",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_Category",
                schema: "mabuntle",
                table: "support_tickets",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_CreatedByUserId",
                schema: "mabuntle",
                table: "support_tickets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedOrderId",
                schema: "mabuntle",
                table: "support_tickets",
                column: "LinkedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedPaymentId",
                schema: "mabuntle",
                table: "support_tickets",
                column: "LinkedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedProductId",
                schema: "mabuntle",
                table: "support_tickets",
                column: "LinkedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_LinkedSellerId",
                schema: "mabuntle",
                table: "support_tickets",
                column: "LinkedSellerId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_OpenedAtUtc",
                schema: "mabuntle",
                table: "support_tickets",
                column: "OpenedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_SellerId",
                schema: "mabuntle",
                table: "support_tickets",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_Status",
                schema: "mabuntle",
                table: "support_tickets",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_messages",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "support_tickets",
                schema: "mabuntle");
        }
    }
}

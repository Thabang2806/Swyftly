using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualShippingFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CarrierName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TrackingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ShippedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipments_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shipments_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_events",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipment_events_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "mabuntle",
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipment_events_EventType",
                schema: "mabuntle",
                table: "shipment_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_events_OccurredAtUtc",
                schema: "mabuntle",
                table: "shipment_events",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_events_ShipmentId",
                schema: "mabuntle",
                table: "shipment_events",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_events_Status",
                schema: "mabuntle",
                table: "shipment_events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_BuyerId",
                schema: "mabuntle",
                table: "shipments",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_CreatedAtUtc",
                schema: "mabuntle",
                table: "shipments",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_OrderId",
                schema: "mabuntle",
                table: "shipments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_SellerId",
                schema: "mabuntle",
                table: "shipments",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_Status",
                schema: "mabuntle",
                table: "shipments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_TrackingNumber",
                schema: "mabuntle",
                table: "shipments",
                column: "TrackingNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipment_events",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "mabuntle");
        }
    }
}

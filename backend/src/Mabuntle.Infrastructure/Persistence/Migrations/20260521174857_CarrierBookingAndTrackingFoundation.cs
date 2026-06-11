using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CarrierBookingAndTrackingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CarrierBookedAtUtc",
                schema: "mabuntle",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierBookingStatus",
                schema: "mabuntle",
                table: "shipments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierProviderName",
                schema: "mabuntle",
                table: "shipments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierServiceCode",
                schema: "mabuntle",
                table: "shipments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageHeightCm",
                schema: "mabuntle",
                table: "shipments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageLengthCm",
                schema: "mabuntle",
                table: "shipments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageWeightKg",
                schema: "mabuntle",
                table: "shipments",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageWidthCm",
                schema: "mabuntle",
                table: "shipments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderError",
                schema: "mabuntle",
                table: "shipments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderLabelUrl",
                schema: "mabuntle",
                table: "shipments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderLastSyncedAtUtc",
                schema: "mabuntle",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderShipmentReference",
                schema: "mabuntle",
                table: "shipments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                schema: "mabuntle",
                table: "shipments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderStatusUpdatedAtUtc",
                schema: "mabuntle",
                table: "shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_CarrierBookingStatus",
                schema: "mabuntle",
                table: "shipments",
                column: "CarrierBookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_ProviderLastSyncedAtUtc",
                schema: "mabuntle",
                table: "shipments",
                column: "ProviderLastSyncedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_ProviderShipmentReference",
                schema: "mabuntle",
                table: "shipments",
                column: "ProviderShipmentReference");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_ProviderStatus",
                schema: "mabuntle",
                table: "shipments",
                column: "ProviderStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shipments_CarrierBookingStatus",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_shipments_ProviderLastSyncedAtUtc",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_shipments_ProviderShipmentReference",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "IX_shipments_ProviderStatus",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "CarrierBookedAtUtc",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "CarrierBookingStatus",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "CarrierProviderName",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "CarrierServiceCode",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "PackageHeightCm",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "PackageLengthCm",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "PackageWeightKg",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "PackageWidthCm",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ProviderError",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ProviderLabelUrl",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ProviderLastSyncedAtUtc",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ProviderShipmentReference",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                schema: "mabuntle",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "ProviderStatusUpdatedAtUtc",
                schema: "mabuntle",
                table: "shipments");
        }
    }
}

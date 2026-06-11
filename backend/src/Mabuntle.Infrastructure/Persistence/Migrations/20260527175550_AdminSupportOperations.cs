using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminSupportOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAtUtc",
                schema: "mabuntle",
                table: "support_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedByUserId",
                schema: "mabuntle",
                table: "support_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationReason",
                schema: "mabuntle",
                table: "support_tickets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                schema: "mabuntle",
                table: "support_tickets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Normal");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EscalatedAtUtc",
                schema: "mabuntle",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "EscalatedByUserId",
                schema: "mabuntle",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "EscalationReason",
                schema: "mabuntle",
                table: "support_tickets");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "mabuntle",
                table: "support_tickets");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerProfileSettingsAndNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "mabuntle",
                table: "buyer_profiles",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "mabuntle",
                table: "buyer_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "buyer_notification_preferences",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buyer_notification_preferences_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_notification_preferences_BuyerId_Category",
                schema: "mabuntle",
                table: "buyer_notification_preferences",
                columns: new[] { "BuyerId", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buyer_notification_preferences",
                schema: "mabuntle");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "mabuntle",
                table: "buyer_profiles");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "mabuntle",
                table: "buyer_profiles");
        }
    }
}

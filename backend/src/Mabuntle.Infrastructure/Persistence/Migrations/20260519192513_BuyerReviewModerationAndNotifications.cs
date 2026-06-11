using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerReviewModerationAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModeratedAtUtc",
                schema: "mabuntle",
                table: "product_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModeratedByUserId",
                schema: "mabuntle",
                table: "product_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                schema: "mabuntle",
                table: "product_reviews",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeratedAtUtc",
                schema: "mabuntle",
                table: "product_reviews");

            migrationBuilder.DropColumn(
                name: "ModeratedByUserId",
                schema: "mabuntle",
                table: "product_reviews");

            migrationBuilder.DropColumn(
                name: "ModerationReason",
                schema: "mabuntle",
                table: "product_reviews");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StorefrontAttributionAndSourceReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferrerHost",
                schema: "swyftly",
                table: "seller_funnel_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCategory",
                schema: "swyftly",
                table: "seller_funnel_events",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmCampaign",
                schema: "swyftly",
                table: "seller_funnel_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmMedium",
                schema: "swyftly",
                table: "seller_funnel_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmSource",
                schema: "swyftly",
                table: "seller_funnel_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_SellerId_SourceCategory_OccurredAtUtc",
                schema: "swyftly",
                table: "seller_funnel_events",
                columns: new[] { "SellerId", "SourceCategory", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_seller_funnel_events_SellerId_SourceCategory_OccurredAtUtc",
                schema: "swyftly",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "ReferrerHost",
                schema: "swyftly",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "SourceCategory",
                schema: "swyftly",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "UtmCampaign",
                schema: "swyftly",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "UtmMedium",
                schema: "swyftly",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "UtmSource",
                schema: "swyftly",
                table: "seller_funnel_events");
        }
    }
}

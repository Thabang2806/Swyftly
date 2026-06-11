using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StorefrontAttributionAndSourceReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferrerHost",
                schema: "mabuntle",
                table: "seller_funnel_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCategory",
                schema: "mabuntle",
                table: "seller_funnel_events",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmCampaign",
                schema: "mabuntle",
                table: "seller_funnel_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmMedium",
                schema: "mabuntle",
                table: "seller_funnel_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmSource",
                schema: "mabuntle",
                table: "seller_funnel_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_funnel_events_SellerId_SourceCategory_OccurredAtUtc",
                schema: "mabuntle",
                table: "seller_funnel_events",
                columns: new[] { "SellerId", "SourceCategory", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_seller_funnel_events_SellerId_SourceCategory_OccurredAtUtc",
                schema: "mabuntle",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "ReferrerHost",
                schema: "mabuntle",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "SourceCategory",
                schema: "mabuntle",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "UtmCampaign",
                schema: "mabuntle",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "UtmMedium",
                schema: "mabuntle",
                table: "seller_funnel_events");

            migrationBuilder.DropColumn(
                name: "UtmSource",
                schema: "mabuntle",
                table: "seller_funnel_events");
        }
    }
}

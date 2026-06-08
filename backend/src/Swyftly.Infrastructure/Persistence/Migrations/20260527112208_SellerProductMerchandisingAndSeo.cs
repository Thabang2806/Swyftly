using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerProductMerchandisingAndSeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CareInstructions",
                schema: "mabuntle",
                table: "products",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchandisingLabel",
                schema: "mabuntle",
                table: "products",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDisclaimer",
                schema: "mabuntle",
                table: "products",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                schema: "mabuntle",
                table: "products",
                type: "character varying(170)",
                maxLength: 170,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                schema: "mabuntle",
                table: "products",
                type: "character varying(70)",
                maxLength: 70,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CareInstructions",
                schema: "mabuntle",
                table: "product_listing_revisions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchandisingLabel",
                schema: "mabuntle",
                table: "product_listing_revisions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDisclaimer",
                schema: "mabuntle",
                table: "product_listing_revisions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                schema: "mabuntle",
                table: "product_listing_revisions",
                type: "character varying(170)",
                maxLength: 170,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                schema: "mabuntle",
                table: "product_listing_revisions",
                type: "character varying(70)",
                maxLength: 70,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CareInstructions",
                schema: "mabuntle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "MerchandisingLabel",
                schema: "mabuntle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductDisclaimer",
                schema: "mabuntle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SeoDescription",
                schema: "mabuntle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                schema: "mabuntle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CareInstructions",
                schema: "mabuntle",
                table: "product_listing_revisions");

            migrationBuilder.DropColumn(
                name: "MerchandisingLabel",
                schema: "mabuntle",
                table: "product_listing_revisions");

            migrationBuilder.DropColumn(
                name: "ProductDisclaimer",
                schema: "mabuntle",
                table: "product_listing_revisions");

            migrationBuilder.DropColumn(
                name: "SeoDescription",
                schema: "mabuntle",
                table: "product_listing_revisions");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                schema: "mabuntle",
                table: "product_listing_revisions");
        }
    }
}

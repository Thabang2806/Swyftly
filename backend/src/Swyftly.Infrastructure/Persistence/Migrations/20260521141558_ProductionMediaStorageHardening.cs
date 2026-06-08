using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionMediaStorageHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MediaAssetId",
                schema: "mabuntle",
                table: "product_listing_revision_images",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaAssetId",
                schema: "mabuntle",
                table: "product_images",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductListingRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bucket = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    ScanStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScannedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeleteRequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_media_assets_product_listing_revisions_ProductListingRevisi~",
                        column: x => x.ProductListingRevisionId,
                        principalSchema: "mabuntle",
                        principalTable: "product_listing_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_media_assets_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_media_assets_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "media_asset_variants",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_asset_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_media_asset_variants_media_assets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalSchema: "mabuntle",
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_revision_images_MediaAssetId",
                schema: "mabuntle",
                table: "product_listing_revision_images",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_product_images_MediaAssetId",
                schema: "mabuntle",
                table: "product_images",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_media_asset_variants_MediaAssetId_Kind",
                schema: "mabuntle",
                table: "media_asset_variants",
                columns: new[] { "MediaAssetId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_asset_variants_StorageKey",
                schema: "mabuntle",
                table: "media_asset_variants",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_LifecycleStatus",
                schema: "mabuntle",
                table: "media_assets",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_ProductId",
                schema: "mabuntle",
                table: "media_assets",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_ProductListingRevisionId",
                schema: "mabuntle",
                table: "media_assets",
                column: "ProductListingRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_ScanStatus",
                schema: "mabuntle",
                table: "media_assets",
                column: "ScanStatus");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_SellerId",
                schema: "mabuntle",
                table: "media_assets",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_StorageKey",
                schema: "mabuntle",
                table: "media_assets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_product_images_media_assets_MediaAssetId",
                schema: "mabuntle",
                table: "product_images",
                column: "MediaAssetId",
                principalSchema: "mabuntle",
                principalTable: "media_assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_product_listing_revision_images_media_assets_MediaAssetId",
                schema: "mabuntle",
                table: "product_listing_revision_images",
                column: "MediaAssetId",
                principalSchema: "mabuntle",
                principalTable: "media_assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_images_media_assets_MediaAssetId",
                schema: "mabuntle",
                table: "product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_product_listing_revision_images_media_assets_MediaAssetId",
                schema: "mabuntle",
                table: "product_listing_revision_images");

            migrationBuilder.DropTable(
                name: "media_asset_variants",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "mabuntle");

            migrationBuilder.DropIndex(
                name: "IX_product_listing_revision_images_MediaAssetId",
                schema: "mabuntle",
                table: "product_listing_revision_images");

            migrationBuilder.DropIndex(
                name: "IX_product_images_MediaAssetId",
                schema: "mabuntle",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "MediaAssetId",
                schema: "mabuntle",
                table: "product_listing_revision_images");

            migrationBuilder.DropColumn(
                name: "MediaAssetId",
                schema: "mabuntle",
                table: "product_images");
        }
    }
}

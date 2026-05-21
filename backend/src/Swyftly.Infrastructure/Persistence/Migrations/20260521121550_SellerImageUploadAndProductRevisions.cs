using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerImageUploadAndProductRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_listing_revisions",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    ShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FullDescription = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    tags_json = table.Column<string>(type: "jsonb", nullable: false),
                    attributes_json = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_listing_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_listing_revisions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "swyftly",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_listing_revisions_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "swyftly",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_listing_revision_images",
                schema: "swyftly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProductImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_listing_revision_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_listing_revision_images_product_listing_revisions_R~",
                        column: x => x.RevisionId,
                        principalSchema: "swyftly",
                        principalTable: "product_listing_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_revision_images_RevisionId_IsPrimary",
                schema: "swyftly",
                table: "product_listing_revision_images",
                columns: new[] { "RevisionId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_revision_images_RevisionId_SortOrder",
                schema: "swyftly",
                table: "product_listing_revision_images",
                columns: new[] { "RevisionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_revisions_ProductId",
                schema: "swyftly",
                table: "product_listing_revisions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_revisions_ProductId_Status",
                schema: "swyftly",
                table: "product_listing_revisions",
                columns: new[] { "ProductId", "Status" },
                unique: true,
                filter: "\"Status\" IN ('Draft', 'PendingReview', 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_revisions_SellerId",
                schema: "swyftly",
                table: "product_listing_revisions",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_product_listing_revisions_Status",
                schema: "swyftly",
                table: "product_listing_revisions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_listing_revision_images",
                schema: "swyftly");

            migrationBuilder.DropTable(
                name: "product_listing_revisions",
                schema: "swyftly");
        }
    }
}

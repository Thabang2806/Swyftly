using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublishedVariantAndPricingRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_variant_revisions",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SellerReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variant_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_variant_revisions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_variant_revisions_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_revision_items",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Size = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Colour = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CompareAtPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InitialStockQuantity = table.Column<int>(type: "integer", nullable: true),
                    ProposedStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variant_revision_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_variant_revision_items_product_variant_revisions_Re~",
                        column: x => x.RevisionId,
                        principalSchema: "mabuntle",
                        principalTable: "product_variant_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_variant_revision_items_product_variants_SourceVaria~",
                        column: x => x.SourceVariantId,
                        principalSchema: "mabuntle",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revision_items_RevisionId",
                schema: "mabuntle",
                table: "product_variant_revision_items",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revision_items_RevisionId_Size_Colour",
                schema: "mabuntle",
                table: "product_variant_revision_items",
                columns: new[] { "RevisionId", "Size", "Colour" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revision_items_RevisionId_Sku",
                schema: "mabuntle",
                table: "product_variant_revision_items",
                columns: new[] { "RevisionId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revision_items_SourceVariantId",
                schema: "mabuntle",
                table: "product_variant_revision_items",
                column: "SourceVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revisions_ProductId",
                schema: "mabuntle",
                table: "product_variant_revisions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revisions_ProductId_Status",
                schema: "mabuntle",
                table: "product_variant_revisions",
                columns: new[] { "ProductId", "Status" },
                unique: true,
                filter: "\"Status\" IN ('Draft', 'PendingReview', 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revisions_SellerId",
                schema: "mabuntle",
                table: "product_variant_revisions",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_revisions_Status",
                schema: "mabuntle",
                table: "product_variant_revisions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_variant_revision_items",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "product_variant_revisions",
                schema: "mabuntle");
        }
    }
}

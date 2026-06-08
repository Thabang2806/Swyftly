using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogCategoriesAndAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_categories_categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalSchema: "mabuntle",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "category_attributes",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DataType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_values_json = table.Column<string>(type: "jsonb", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_category_attributes_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "mabuntle",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "mabuntle",
                table: "categories",
                columns: new[] { "Id", "DisplayOrder", "IsActive", "Name", "ParentCategoryId", "Slug" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), 10, true, "Women", null, "women" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), 20, true, "Men", null, "men" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), 30, true, "Jewellery", null, "jewellery" },
                    { new Guid("20000000-0000-0000-0000-000000000012"), 40, true, "Accessories", null, "accessories" },
                    { new Guid("20000000-0000-0000-0000-000000000015"), 50, true, "Beauty", null, "beauty" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), 10, true, "Clothing", new Guid("20000000-0000-0000-0000-000000000001"), "women-clothing" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), 10, true, "Clothing", new Guid("20000000-0000-0000-0000-000000000005"), "men-clothing" },
                    { new Guid("20000000-0000-0000-0000-000000000009"), 10, true, "Earrings", new Guid("20000000-0000-0000-0000-000000000008"), "jewellery-earrings" },
                    { new Guid("20000000-0000-0000-0000-000000000011"), 20, true, "Rings", new Guid("20000000-0000-0000-0000-000000000008"), "jewellery-rings" },
                    { new Guid("20000000-0000-0000-0000-000000000013"), 10, true, "Bags", new Guid("20000000-0000-0000-0000-000000000012"), "accessories-bags" },
                    { new Guid("20000000-0000-0000-0000-000000000014"), 20, true, "Belts", new Guid("20000000-0000-0000-0000-000000000012"), "accessories-belts" },
                    { new Guid("20000000-0000-0000-0000-000000000016"), 10, true, "Makeup", new Guid("20000000-0000-0000-0000-000000000015"), "beauty-makeup" },
                    { new Guid("20000000-0000-0000-0000-000000000018"), 20, true, "Skincare", new Guid("20000000-0000-0000-0000-000000000015"), "beauty-skincare" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), 10, true, "Dresses", new Guid("20000000-0000-0000-0000-000000000002"), "women-clothing-dresses" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), 20, true, "Tops", new Guid("20000000-0000-0000-0000-000000000002"), "women-clothing-tops" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), 10, true, "Shirts", new Guid("20000000-0000-0000-0000-000000000006"), "men-clothing-shirts" },
                    { new Guid("20000000-0000-0000-0000-000000000010"), 10, true, "Hoop Earrings", new Guid("20000000-0000-0000-0000-000000000009"), "jewellery-earrings-hoop-earrings" },
                    { new Guid("20000000-0000-0000-0000-000000000017"), 10, true, "Foundation", new Guid("20000000-0000-0000-0000-000000000016"), "beauty-makeup-foundation" },
                    { new Guid("20000000-0000-0000-0000-000000000019"), 10, true, "Cleansers", new Guid("20000000-0000-0000-0000-000000000018"), "beauty-skincare-cleansers" }
                });

            migrationBuilder.InsertData(
                schema: "mabuntle",
                table: "category_attributes",
                columns: new[] { "Id", "allowed_values_json", "CategoryId", "DataType", "DisplayOrder", "IsActive", "IsRequired", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000011"), "[\"Gold\",\"Silver\",\"Stainless Steel\"]", new Guid("20000000-0000-0000-0000-000000000011"), "Select", 10, true, true, "material", "Material" },
                    { new Guid("30000000-0000-0000-0000-000000000012"), "[\"5\",\"6\",\"7\",\"8\",\"9\",\"10\"]", new Guid("20000000-0000-0000-0000-000000000011"), "Select", 20, true, true, "ring-size", "Ring Size" },
                    { new Guid("30000000-0000-0000-0000-000000000013"), "[\"Leather\",\"Vegan Leather\",\"Canvas\",\"Fabric\"]", new Guid("20000000-0000-0000-0000-000000000013"), "Select", 10, true, true, "material", "Material" },
                    { new Guid("30000000-0000-0000-0000-000000000014"), "[]", new Guid("20000000-0000-0000-0000-000000000013"), "Text", 20, true, true, "colour", "Colour" },
                    { new Guid("30000000-0000-0000-0000-000000000015"), "[\"S\",\"M\",\"L\",\"XL\"]", new Guid("20000000-0000-0000-0000-000000000014"), "Select", 10, true, true, "size", "Size" },
                    { new Guid("30000000-0000-0000-0000-000000000016"), "[\"Leather\",\"Vegan Leather\",\"Fabric\"]", new Guid("20000000-0000-0000-0000-000000000014"), "Select", 20, true, true, "material", "Material" },
                    { new Guid("30000000-0000-0000-0000-000000000001"), "[\"XS\",\"S\",\"M\",\"L\",\"XL\"]", new Guid("20000000-0000-0000-0000-000000000003"), "Select", 10, true, true, "size", "Size" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "[]", new Guid("20000000-0000-0000-0000-000000000003"), "Text", 20, true, true, "colour", "Colour" },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "[]", new Guid("20000000-0000-0000-0000-000000000003"), "Text", 30, true, false, "material", "Material" },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "[\"XS\",\"S\",\"M\",\"L\",\"XL\"]", new Guid("20000000-0000-0000-0000-000000000004"), "Select", 10, true, true, "size", "Size" },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "[]", new Guid("20000000-0000-0000-0000-000000000004"), "Text", 20, true, true, "colour", "Colour" },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "[\"S\",\"M\",\"L\",\"XL\",\"XXL\"]", new Guid("20000000-0000-0000-0000-000000000007"), "Select", 10, true, true, "size", "Size" },
                    { new Guid("30000000-0000-0000-0000-000000000007"), "[]", new Guid("20000000-0000-0000-0000-000000000007"), "Text", 20, true, true, "colour", "Colour" },
                    { new Guid("30000000-0000-0000-0000-000000000008"), "[]", new Guid("20000000-0000-0000-0000-000000000007"), "Text", 30, true, false, "fit", "Fit" },
                    { new Guid("30000000-0000-0000-0000-000000000009"), "[\"Gold\",\"Silver\",\"Stainless Steel\",\"Beaded\"]", new Guid("20000000-0000-0000-0000-000000000010"), "Select", 10, true, true, "material", "Material" },
                    { new Guid("30000000-0000-0000-0000-000000000010"), "[]", new Guid("20000000-0000-0000-0000-000000000010"), "Text", 20, true, false, "colour", "Colour" },
                    { new Guid("30000000-0000-0000-0000-000000000017"), "[]", new Guid("20000000-0000-0000-0000-000000000017"), "Text", 10, true, true, "shade", "Shade" },
                    { new Guid("30000000-0000-0000-0000-000000000018"), "[\"Dry\",\"Oily\",\"Combination\",\"Sensitive\"]", new Guid("20000000-0000-0000-0000-000000000017"), "MultiSelect", 20, true, false, "skin-type", "Skin Type" },
                    { new Guid("30000000-0000-0000-0000-000000000019"), "[]", new Guid("20000000-0000-0000-0000-000000000017"), "Decimal", 30, true, false, "volume-ml", "Volume ml" },
                    { new Guid("30000000-0000-0000-0000-000000000020"), "[\"Dry\",\"Oily\",\"Combination\",\"Sensitive\"]", new Guid("20000000-0000-0000-0000-000000000019"), "MultiSelect", 10, true, true, "skin-type", "Skin Type" },
                    { new Guid("30000000-0000-0000-0000-000000000021"), "[]", new Guid("20000000-0000-0000-0000-000000000019"), "Decimal", 20, true, false, "volume-ml", "Volume ml" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_ParentCategoryId_DisplayOrder",
                schema: "mabuntle",
                table: "categories",
                columns: new[] { "ParentCategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_Slug",
                schema: "mabuntle",
                table: "categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_attributes_CategoryId_DisplayOrder",
                schema: "mabuntle",
                table: "category_attributes",
                columns: new[] { "CategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_category_attributes_CategoryId_Key",
                schema: "mabuntle",
                table: "category_attributes",
                columns: new[] { "CategoryId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_attributes",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "mabuntle");
        }
    }
}

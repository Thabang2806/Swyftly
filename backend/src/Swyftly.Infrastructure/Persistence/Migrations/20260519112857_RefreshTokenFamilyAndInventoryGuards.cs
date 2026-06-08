using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenFamilyAndInventoryGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "family_id",
                schema: "mabuntle",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE mabuntle.refresh_tokens SET family_id = \"Id\" WHERE family_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "family_id",
                schema: "mabuntle",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revoked_reason",
                schema: "mabuntle",
                table: "refresh_tokens",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId_family_id",
                schema: "mabuntle",
                table: "refresh_tokens",
                columns: new[] { "UserId", "family_id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_variants_reserved_quantity_non_negative",
                schema: "mabuntle",
                table: "product_variants",
                sql: "\"ReservedQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_variants_reserved_quantity_not_above_stock",
                schema: "mabuntle",
                table: "product_variants",
                sql: "\"ReservedQuantity\" <= \"StockQuantity\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_UserId_family_id",
                schema: "mabuntle",
                table: "refresh_tokens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_variants_reserved_quantity_non_negative",
                schema: "mabuntle",
                table: "product_variants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_variants_reserved_quantity_not_above_stock",
                schema: "mabuntle",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "family_id",
                schema: "mabuntle",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "revoked_reason",
                schema: "mabuntle",
                table: "refresh_tokens");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductEmbeddingsPgvector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "product_embeddings",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceText = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    ModelUsed = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_embeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_embeddings_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_embeddings_CreatedAtUtc",
                schema: "mabuntle",
                table: "product_embeddings",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_product_embeddings_ModelUsed",
                schema: "mabuntle",
                table: "product_embeddings",
                column: "ModelUsed");

            migrationBuilder.CreateIndex(
                name: "IX_product_embeddings_ProductId",
                schema: "mabuntle",
                table: "product_embeddings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_embeddings_ProductId_ModelUsed",
                schema: "mabuntle",
                table: "product_embeddings",
                columns: new[] { "ProductId", "ModelUsed" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_embeddings",
                schema: "mabuntle");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}

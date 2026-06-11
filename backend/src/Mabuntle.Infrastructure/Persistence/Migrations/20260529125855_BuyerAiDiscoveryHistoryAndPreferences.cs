using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BuyerAiDiscoveryHistoryAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "buyer_ai_discovery_history",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTool = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Colour = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Material = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConfidenceBand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    ProductIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    SourceRoute = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_ai_discovery_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buyer_ai_discovery_history_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "buyer_ai_discovery_preferences",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    HistoryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyer_ai_discovery_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buyer_ai_discovery_preferences_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_ai_discovery_history_BuyerId_CreatedAtUtc",
                schema: "mabuntle",
                table: "buyer_ai_discovery_history",
                columns: new[] { "BuyerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_ai_discovery_history_BuyerId_SourceTool_CreatedAtUtc",
                schema: "mabuntle",
                table: "buyer_ai_discovery_history",
                columns: new[] { "BuyerId", "SourceTool", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_buyer_ai_discovery_preferences_BuyerId",
                schema: "mabuntle",
                table: "buyer_ai_discovery_preferences",
                column: "BuyerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buyer_ai_discovery_history",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "buyer_ai_discovery_preferences",
                schema: "mabuntle");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerProfileAndVerificationDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessName",
                schema: "mabuntle",
                table: "seller_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "mabuntle",
                table: "seller_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "mabuntle",
                table: "seller_profiles",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "mabuntle",
                table: "seller_profiles",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "mabuntle",
                table: "seller_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seller_addresses",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Province = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_addresses_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seller_payout_profiles",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutProviderReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HasSubmittedPlaceholder = table.Column<bool>(type: "boolean", nullable: false),
                    IsAdminApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_payout_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_payout_profiles_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seller_storefronts",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    BannerUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_storefronts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_storefronts_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seller_verifications",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_verifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_verifications_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_addresses_SellerId",
                schema: "mabuntle",
                table: "seller_addresses",
                column: "SellerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_payout_profiles_SellerId",
                schema: "mabuntle",
                table: "seller_payout_profiles",
                column: "SellerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_storefronts_SellerId",
                schema: "mabuntle",
                table: "seller_storefronts",
                column: "SellerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_storefronts_Slug",
                schema: "mabuntle",
                table: "seller_storefronts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_verifications_SellerId",
                schema: "mabuntle",
                table: "seller_verifications",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_addresses",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "seller_payout_profiles",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "seller_storefronts",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "seller_verifications",
                schema: "mabuntle");

            migrationBuilder.DropColumn(
                name: "BusinessName",
                schema: "mabuntle",
                table: "seller_profiles");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                schema: "mabuntle",
                table: "seller_profiles");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "mabuntle",
                table: "seller_profiles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "mabuntle",
                table: "seller_profiles");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "mabuntle",
                table: "seller_profiles");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdCampaignFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ad_campaigns",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CampaignType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PausedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_campaigns_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seller_ad_credits",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_ad_credits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_ad_credits_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ad_budgets",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DailyBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxCostPerClick = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SpentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_budgets_ad_campaigns_AdCampaignId",
                        column: x => x.AdCampaignId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ad_campaign_products",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_campaign_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_campaign_products_ad_campaigns_AdCampaignId",
                        column: x => x.AdCampaignId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ad_campaign_products_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ad_clicks",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnonymousVisitorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_clicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_clicks_ad_campaigns_AdCampaignId",
                        column: x => x.AdCampaignId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ad_clicks_buyer_profiles_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "mabuntle",
                        principalTable: "buyer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ad_clicks_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ad_impressions",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Placement = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AnonymousVisitorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_impressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_impressions_ad_campaigns_AdCampaignId",
                        column: x => x.AdCampaignId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ad_impressions_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "mabuntle",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ad_charges",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdClickId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ChargedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_charges_ad_campaigns_AdCampaignId",
                        column: x => x.AdCampaignId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ad_charges_ad_clicks_AdClickId",
                        column: x => x.AdClickId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_clicks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ad_conversions",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdClickId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevenueAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_conversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ad_conversions_ad_campaigns_AdCampaignId",
                        column: x => x.AdCampaignId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ad_conversions_ad_clicks_AdClickId",
                        column: x => x.AdClickId,
                        principalSchema: "mabuntle",
                        principalTable: "ad_clicks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ad_conversions_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "mabuntle",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ad_budgets_AdCampaignId",
                schema: "mabuntle",
                table: "ad_budgets",
                column: "AdCampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaign_products_AdCampaignId_ProductId",
                schema: "mabuntle",
                table: "ad_campaign_products",
                columns: new[] { "AdCampaignId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaign_products_ProductId",
                schema: "mabuntle",
                table: "ad_campaign_products",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_CampaignType",
                schema: "mabuntle",
                table: "ad_campaigns",
                column: "CampaignType");

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_SellerId",
                schema: "mabuntle",
                table: "ad_campaigns",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_StartsAtUtc_EndsAtUtc",
                schema: "mabuntle",
                table: "ad_campaigns",
                columns: new[] { "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_Status",
                schema: "mabuntle",
                table: "ad_campaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ad_charges_AdCampaignId",
                schema: "mabuntle",
                table: "ad_charges",
                column: "AdCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_charges_AdClickId",
                schema: "mabuntle",
                table: "ad_charges",
                column: "AdClickId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_charges_ChargedAtUtc",
                schema: "mabuntle",
                table: "ad_charges",
                column: "ChargedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ad_clicks_AdCampaignId",
                schema: "mabuntle",
                table: "ad_clicks",
                column: "AdCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_clicks_BuyerId",
                schema: "mabuntle",
                table: "ad_clicks",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_clicks_OccurredAtUtc",
                schema: "mabuntle",
                table: "ad_clicks",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ad_clicks_ProductId",
                schema: "mabuntle",
                table: "ad_clicks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_conversions_AdCampaignId",
                schema: "mabuntle",
                table: "ad_conversions",
                column: "AdCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_conversions_AdClickId",
                schema: "mabuntle",
                table: "ad_conversions",
                column: "AdClickId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_conversions_OccurredAtUtc",
                schema: "mabuntle",
                table: "ad_conversions",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ad_conversions_OrderId",
                schema: "mabuntle",
                table: "ad_conversions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_impressions_AdCampaignId",
                schema: "mabuntle",
                table: "ad_impressions",
                column: "AdCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_impressions_AdCampaignId_AnonymousVisitorId_OccurredAtUtc",
                schema: "mabuntle",
                table: "ad_impressions",
                columns: new[] { "AdCampaignId", "AnonymousVisitorId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ad_impressions_OccurredAtUtc",
                schema: "mabuntle",
                table: "ad_impressions",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ad_impressions_ProductId",
                schema: "mabuntle",
                table: "ad_impressions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_ad_credits_SellerId_Currency",
                schema: "mabuntle",
                table: "seller_ad_credits",
                columns: new[] { "SellerId", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_budgets",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ad_campaign_products",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ad_charges",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ad_conversions",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ad_impressions",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "seller_ad_credits",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ad_clicks",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "ad_campaigns",
                schema: "mabuntle");
        }
    }
}

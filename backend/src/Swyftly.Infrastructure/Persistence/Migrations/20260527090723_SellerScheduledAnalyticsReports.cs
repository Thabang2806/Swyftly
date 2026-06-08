using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerScheduledAnalyticsReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "seller_report_schedules",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReportRange = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SendDayOfWeek = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SendDayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    SendTimeLocal = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NextRunAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReportPeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReportPeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastFailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_report_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_report_schedules_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "seller_report_schedule_runs",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerReportScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportPeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReportPeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller_report_schedule_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seller_report_schedule_runs_seller_profiles_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_seller_report_schedule_runs_seller_report_schedules_SellerR~",
                        column: x => x.SellerReportScheduleId,
                        principalSchema: "mabuntle",
                        principalTable: "seller_report_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_seller_report_schedule_runs_SellerId",
                schema: "mabuntle",
                table: "seller_report_schedule_runs",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_seller_report_schedule_runs_SellerReportScheduleId_ReportPe~",
                schema: "mabuntle",
                table: "seller_report_schedule_runs",
                columns: new[] { "SellerReportScheduleId", "ReportPeriodStartUtc", "ReportPeriodEndUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seller_report_schedules_NextRunAtUtc",
                schema: "mabuntle",
                table: "seller_report_schedules",
                column: "NextRunAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_seller_report_schedules_SellerId",
                schema: "mabuntle",
                table: "seller_report_schedules",
                column: "SellerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "seller_report_schedule_runs",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "seller_report_schedules",
                schema: "mabuntle");
        }
    }
}

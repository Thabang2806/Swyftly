using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminQueueSavedViewsAndSlaSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_queue_saved_views",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Queue = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    View = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Search = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Assigned = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Priority = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    HasNotes = table.Column<bool>(type: "boolean", nullable: true),
                    Sla = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Sort = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PageSize = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_queue_saved_views", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_queue_saved_views_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalSchema: "mabuntle",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_saved_views_AdminUserId_Queue_IsDefault",
                schema: "mabuntle",
                table: "admin_queue_saved_views",
                columns: new[] { "AdminUserId", "Queue", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_saved_views_AdminUserId_Queue_Name",
                schema: "mabuntle",
                table: "admin_queue_saved_views",
                columns: new[] { "AdminUserId", "Queue", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_queue_saved_views",
                schema: "mabuntle");
        }
    }
}

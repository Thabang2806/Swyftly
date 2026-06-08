using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swyftly.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminOperationalQueueTriage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_queue_triage",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LatestNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LatestNoteByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LatestNoteAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_queue_triage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_queue_triage_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalSchema: "mabuntle",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "admin_queue_triage_notes",
                schema: "mabuntle",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TriageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_queue_triage_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_queue_triage_notes_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "mabuntle",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_queue_triage_notes_admin_queue_triage_TriageId",
                        column: x => x.TriageId,
                        principalSchema: "mabuntle",
                        principalTable: "admin_queue_triage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_triage_AssignedToUserId",
                schema: "mabuntle",
                table: "admin_queue_triage",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_triage_ItemType_ItemId",
                schema: "mabuntle",
                table: "admin_queue_triage",
                columns: new[] { "ItemType", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_triage_Priority",
                schema: "mabuntle",
                table: "admin_queue_triage",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_triage_notes_ActorUserId",
                schema: "mabuntle",
                table: "admin_queue_triage_notes",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_triage_notes_CreatedAtUtc",
                schema: "mabuntle",
                table: "admin_queue_triage_notes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_admin_queue_triage_notes_TriageId",
                schema: "mabuntle",
                table: "admin_queue_triage_notes",
                column: "TriageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_queue_triage_notes",
                schema: "mabuntle");

            migrationBuilder.DropTable(
                name: "admin_queue_triage",
                schema: "mabuntle");
        }
    }
}

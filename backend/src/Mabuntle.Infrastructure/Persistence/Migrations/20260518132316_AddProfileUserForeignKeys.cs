using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mabuntle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileUserForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_buyer_profiles_AspNetUsers_UserId",
                schema: "mabuntle",
                table: "buyer_profiles",
                column: "UserId",
                principalSchema: "mabuntle",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_seller_profiles_AspNetUsers_UserId",
                schema: "mabuntle",
                table: "seller_profiles",
                column: "UserId",
                principalSchema: "mabuntle",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_buyer_profiles_AspNetUsers_UserId",
                schema: "mabuntle",
                table: "buyer_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_seller_profiles_AspNetUsers_UserId",
                schema: "mabuntle",
                table: "seller_profiles");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Despacha.Api.Modules.Platform.Internal.Migrations
{
    /// <inheritdoc />
    public partial class LegacyUserReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LegacyUserId",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_LegacyUserId",
                table: "users",
                column: "LegacyUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_LegacyUserId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LegacyUserId",
                table: "users");
        }
    }
}

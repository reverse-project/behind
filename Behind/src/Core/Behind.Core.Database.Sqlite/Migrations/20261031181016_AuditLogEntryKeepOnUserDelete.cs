using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogEntryKeepOnUserDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_Users_BehalfOfId",
                table: "AuditLogEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_Users_UserId",
                table: "AuditLogEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_Users_BehalfOfId",
                table: "AuditLogEntries",
                column: "BehalfOfId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_Users_UserId",
                table: "AuditLogEntries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_Users_BehalfOfId",
                table: "AuditLogEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_Users_UserId",
                table: "AuditLogEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_Users_BehalfOfId",
                table: "AuditLogEntries",
                column: "BehalfOfId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_Users_UserId",
                table: "AuditLogEntries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}

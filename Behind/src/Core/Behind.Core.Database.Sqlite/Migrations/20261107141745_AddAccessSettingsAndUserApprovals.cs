using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessSettingsAndUserApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessSettings",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    RestrictionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RestrictedToRole = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RestrictionMessage = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    ModeratedSignUp = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessSettings", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "PendingUserApprovals",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingUserApprovals", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_PendingUserApprovals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessSettings");

            migrationBuilder.DropTable(
                name: "PendingUserApprovals");
        }
    }
}

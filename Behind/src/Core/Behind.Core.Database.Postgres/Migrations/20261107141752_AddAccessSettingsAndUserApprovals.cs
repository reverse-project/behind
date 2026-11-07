using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations
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
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    RestrictionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RestrictedToRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RestrictionMessage = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    ModeratedSignUp = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessSettings", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "PendingUserApprovals",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
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

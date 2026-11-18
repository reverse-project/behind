using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261116103131_PersistOidcAccounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsManuallyAssigned",
            table: "UserRoles",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateTable(
            name: "OidcAccountBindings",
            columns: table => new
            {
                ProviderDigest = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                IssuerDigest = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                SubjectDigest = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OidcAccountBindings", x => new { x.ProviderDigest, x.IssuerDigest, x.SubjectDigest });
                table.ForeignKey(
                    name: "FK_OidcAccountBindings_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OidcRoleOwnerships",
            columns: table => new
            {
                ProviderDigest = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OidcRoleOwnerships", x => new { x.ProviderDigest, x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_OidcRoleOwnerships_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_OidcRoleOwnerships_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OidcAccountBindings_UserId",
            table: "OidcAccountBindings",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_OidcRoleOwnerships_RoleId",
            table: "OidcRoleOwnerships",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_OidcRoleOwnerships_UserId_RoleId",
            table: "OidcRoleOwnerships",
            columns: new[] { "UserId", "RoleId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OidcAccountBindings");

        migrationBuilder.DropTable(
            name: "OidcRoleOwnerships");

        migrationBuilder.DropColumn(
            name: "IsManuallyAssigned",
            table: "UserRoles");
    }
}

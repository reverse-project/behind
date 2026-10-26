using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class RemoveDiscriminatorFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_User_BehalfOfId",
                table: "AuditLogEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogEntries_User_UserId",
                table: "AuditLogEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_User_HumanUserId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_User_UserId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleGrants_Role_RoleId",
                table: "RoleGrants");

            migrationBuilder.DropForeignKey(
                name: "FK_User_WallOfShameEntries_GivenById",
                table: "User");

            migrationBuilder.DropForeignKey(
                name: "FK_User_WallOfShameEntries_UserId",
                table: "User");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Role_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_User_UserId",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_User",
                table: "User");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Role",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Role");

            migrationBuilder.RenameTable(
                name: "User",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "Role",
                newName: "Roles");

            migrationBuilder.RenameIndex(
                name: "IX_User_UserId",
                table: "Users",
                newName: "IX_Users_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_User_GivenById",
                table: "Users",
                newName: "IX_Users_GivenById");

            migrationBuilder.RenameIndex(
                name: "IX_Role_Name",
                table: "Roles",
                newName: "IX_Roles_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                table: "Roles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_Users_HumanUserId",
                table: "Emails",
                column: "HumanUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_WallOfShameEntries_GivenById",
                table: "Users",
                column: "GivenById",
                principalTable: "WallOfShameEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_WallOfShameEntries_UserId",
                table: "Users",
                column: "UserId",
                principalTable: "WallOfShameEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Emails_Users_HumanUserId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_WallOfShameEntries_GivenById",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_WallOfShameEntries_UserId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                table: "Roles");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "User");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "Role");

            migrationBuilder.RenameIndex(
                name: "IX_Users_UserId",
                table: "User",
                newName: "IX_User_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_GivenById",
                table: "User",
                newName: "IX_User_GivenById");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_Name",
                table: "Role",
                newName: "IX_Role_Name");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "User",
                type: "TEXT",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Role",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_User",
                table: "User",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Role",
                table: "Role",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_User_BehalfOfId",
                table: "AuditLogEntries",
                column: "BehalfOfId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogEntries_User_UserId",
                table: "AuditLogEntries",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_User_HumanUserId",
                table: "Emails",
                column: "HumanUserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_User_UserId",
                table: "Emails",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleGrants_Role_RoleId",
                table: "RoleGrants",
                column: "RoleId",
                principalTable: "Role",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_WallOfShameEntries_GivenById",
                table: "User",
                column: "GivenById",
                principalTable: "WallOfShameEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_WallOfShameEntries_UserId",
                table: "User",
                column: "UserId",
                principalTable: "WallOfShameEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Role_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Role",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_User_UserId",
                table: "UserRoles",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

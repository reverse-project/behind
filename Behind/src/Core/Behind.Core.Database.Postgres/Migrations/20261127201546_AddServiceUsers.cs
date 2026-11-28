using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261127201546_AddServiceUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Articles_Users_AuthorId",
            table: "Articles");

        migrationBuilder.DropForeignKey(
            name: "FK_AuditLogEntries_Users_BehalfOfId",
            table: "AuditLogEntries");

        migrationBuilder.DropForeignKey(
            name: "FK_AuditLogEntries_Users_UserId",
            table: "AuditLogEntries");

        migrationBuilder.DropForeignKey(
            name: "FK_Downloads_Users_UploadedById",
            table: "Downloads");

        migrationBuilder.DropForeignKey(
            name: "FK_Galleries_Users_AuthorId",
            table: "Galleries");

        migrationBuilder.DropForeignKey(
            name: "FK_News_Users_AuthorId",
            table: "News");

        migrationBuilder.DropForeignKey(
            name: "FK_NewsComments_Users_AuthorId",
            table: "NewsComments");

        migrationBuilder.DropForeignKey(
            name: "FK_UserGrants_Users_UserId",
            table: "UserGrants");

        migrationBuilder.DropForeignKey(
            name: "FK_UserRestrictions_Users_UserId",
            table: "UserRestrictions");

        migrationBuilder.DropForeignKey(
            name: "FK_UserRoles_Users_UserId",
            table: "UserRoles");

        migrationBuilder.DropForeignKey(
            name: "FK_WallOfShameEntries_Users_GivenById",
            table: "WallOfShameEntries");

        migrationBuilder.DropForeignKey(
            name: "FK_WallOfShameEntries_Users_UserId",
            table: "WallOfShameEntries");

        migrationBuilder.CreateTable(
            name: "ServiceUserVersions",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ApiKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceUserVersions", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ServiceUserVersions_Name",
            table: "ServiceUserVersions",
            column: "Name");

        migrationBuilder.Sql("""
            CREATE VIEW "ServiceUsers" AS
            SELECT versioned."Key" AS "Id", versioned."Name", versioned."ApiKeyHash", versioned."Enabled", versioned."Version"
            FROM "ServiceUserVersions" AS versioned
            INNER JOIN (
                SELECT "Key", MAX("Version") AS "Version"
                FROM "ServiceUserVersions"
                GROUP BY "Key"
            ) AS latest ON latest."Key" = versioned."Key" AND latest."Version" = versioned."Version";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        EnsureDowngradeBlocked();

        migrationBuilder.Sql("""DROP VIEW "ServiceUsers";""");
        migrationBuilder.DropTable(name: "ServiceUserVersions");

        migrationBuilder.AddForeignKey(
            name: "FK_Articles_Users_AuthorId",
            table: "Articles",
            column: "AuthorId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

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

        migrationBuilder.AddForeignKey(
            name: "FK_Downloads_Users_UploadedById",
            table: "Downloads",
            column: "UploadedById",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Galleries_Users_AuthorId",
            table: "Galleries",
            column: "AuthorId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_News_Users_AuthorId",
            table: "News",
            column: "AuthorId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_NewsComments_Users_AuthorId",
            table: "NewsComments",
            column: "AuthorId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_UserGrants_Users_UserId",
            table: "UserGrants",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_UserRestrictions_Users_UserId",
            table: "UserRestrictions",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_UserRoles_Users_UserId",
            table: "UserRoles",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_WallOfShameEntries_Users_GivenById",
            table: "WallOfShameEntries",
            column: "GivenById",
            principalTable: "Users",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_WallOfShameEntries_Users_UserId",
            table: "WallOfShameEntries",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    private static void EnsureDowngradeBlocked() =>
        throw new InvalidOperationException(
            "Downgrading service users is blocked because service identities and their references cannot be remapped safely.");
}

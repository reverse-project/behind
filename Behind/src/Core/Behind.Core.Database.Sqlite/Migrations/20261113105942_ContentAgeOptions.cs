using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

public partial class _20261113105942_ContentAgeOptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContentAgeOptions",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                Age = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentAgeOptions", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ContentAgeOptions_Age",
            table: "ContentAgeOptions",
            column: "Age");

        migrationBuilder.Sql("""
            INSERT INTO "ContentAgeOptions" ("Key", "Version", "Age")
            SELECT 'A6E00000-0000-0000-0000-' || printf('%012d', "Age"), 1, "Age"
            FROM (
                SELECT 12 AS "Age"
                UNION SELECT 16
                UNION SELECT 18
                UNION SELECT "MinimumAge" FROM "ContentRestrictions" WHERE "MinimumAge" > 0
            );
            INSERT INTO "AuditLogEntries" ("Id", "EntityName", "EntityKey", "Action", "Timestamp", "UserId", "BehalfOfId")
            SELECT 'B6E00000-0000-0000-0000-' || printf('%012d', "Age"),
                   'ContentAgeOption', lower("Key") || ':1', 'Created',
                   strftime('%Y-%m-%d %H:%M:%f+00:00', 'now'), NULL, NULL
            FROM "ContentAgeOptions";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ContentAgeOptions");
    }
}

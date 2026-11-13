using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

public partial class _20261113105945_ContentAgeOptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContentAgeOptions",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Age = table.Column<int>(type: "integer", nullable: false)
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
            SELECT ('a6e00000-0000-0000-0000-' || lpad("Age"::text, 12, '0'))::uuid, 1, "Age"
            FROM (
                SELECT 12 AS "Age"
                UNION SELECT 16
                UNION SELECT 18
                UNION SELECT "MinimumAge" FROM "ContentRestrictions" WHERE "MinimumAge" > 0
            ) AS ages;
            INSERT INTO "AuditLogEntries" ("Id", "EntityName", "EntityKey", "Action", "Timestamp", "UserId", "BehalfOfId")
            SELECT ('b6e00000-0000-0000-0000-' || lpad("Age"::text, 12, '0'))::uuid,
                   'ContentAgeOption', "Key"::text || ':1', 'Created', CURRENT_TIMESTAMP, NULL, NULL
            FROM "ContentAgeOptions";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ContentAgeOptions");
    }
}

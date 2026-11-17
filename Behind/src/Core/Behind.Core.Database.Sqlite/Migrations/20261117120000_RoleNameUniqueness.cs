using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite.Migrations;

[DbContext(typeof(CoreContextSqlite))]
[Migration("20261117120000_RoleNameUniqueness")]
public sealed class RoleNameUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RoleNameConflictMigration.SqlitePreflightSql);
        migrationBuilder.DropIndex(name: "IX_Roles_Name", table: "Roles");
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX "IX_Roles_Name" ON "Roles" ("Name" COLLATE BINARY)
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Roles_Name", table: "Roles");
        migrationBuilder.CreateIndex(name: "IX_Roles_Name", table: "Roles", column: "Name");
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Postgres.Migrations;

[DbContext(typeof(CoreContextPostgres))]
[Migration("20261117120000_RoleNameUniqueness")]
public sealed partial class RoleNameUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RoleNameConflictMigration.PostgresPreflightSql);
        migrationBuilder.DropIndex(name: "IX_Roles_Name", table: "Roles");
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX "IX_Roles_Name" ON "Roles" ("Name" COLLATE "C")
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Roles_Name", table: "Roles");
        migrationBuilder.CreateIndex(name: "IX_Roles_Name", table: "Roles", column: "Name");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

public partial class _20261117075301_OrdinalRoleNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ReplaceColumn(migrationBuilder, "\"BehindOrdinalIgnoreCaseKey\"(\"Name\")");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ReplaceColumn(migrationBuilder, "upper(\"Name\")");
    }

    private static void ReplaceColumn(MigrationBuilder migrationBuilder, string expression)
    {
        migrationBuilder.DropIndex(name: "UX_Roles_NormalizedName", table: "Roles");
        migrationBuilder.Sql("ALTER TABLE \"Roles\" DROP COLUMN \"NormalizedName\"");
        migrationBuilder.Sql($"ALTER TABLE \"Roles\" ADD COLUMN \"NormalizedName\" TEXT COLLATE BINARY GENERATED ALWAYS AS ({expression}) VIRTUAL");
        migrationBuilder.Sql(RoleNameConflictMigration.SqlitePreflightSql);
        migrationBuilder.CreateIndex(
            name: "UX_Roles_NormalizedName", table: "Roles", column: "NormalizedName", unique: true);
    }
}

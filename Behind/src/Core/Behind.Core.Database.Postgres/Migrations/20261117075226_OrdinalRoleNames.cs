using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

public partial class _20261117075226_OrdinalRoleNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(RoleNameKeyMigration.CreatePostgresFunctionSql());
        ReplaceColumn(migrationBuilder, "\"BehindOrdinalIgnoreCaseKey\"(\"Name\")", "C");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ReplaceColumn(migrationBuilder, "upper(\"Name\")", null);
        migrationBuilder.Sql("DROP FUNCTION \"BehindOrdinalIgnoreCaseKey\"(text)");
    }

    private static void ReplaceColumn(MigrationBuilder migrationBuilder, string expression, string collation)
    {
        migrationBuilder.DropIndex(name: "UX_Roles_NormalizedName", table: "Roles");
        migrationBuilder.DropColumn(name: "NormalizedName", table: "Roles");
        migrationBuilder.AddColumn<string>(
            name: "NormalizedName", table: "Roles", type: "text", nullable: true,
            computedColumnSql: expression, stored: true, collation: collation);
        migrationBuilder.Sql(RoleNameConflictMigration.PostgresPreflightSql);
        migrationBuilder.CreateIndex(
            name: "UX_Roles_NormalizedName", table: "Roles", column: "NormalizedName", unique: true);
    }
}

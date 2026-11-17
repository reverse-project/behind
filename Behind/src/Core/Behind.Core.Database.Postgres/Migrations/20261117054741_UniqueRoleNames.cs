using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261117054741_UniqueRoleNames : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NormalizedName",
            table: "Roles",
            type: "text",
            nullable: true,
            computedColumnSql: "upper(\"Name\")",
            stored: true);

        migrationBuilder.CreateIndex(
            name: "UX_Roles_NormalizedName",
            table: "Roles",
            column: "NormalizedName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_Roles_NormalizedName",
            table: "Roles");

        migrationBuilder.DropColumn(
            name: "NormalizedName",
            table: "Roles");
    }
}

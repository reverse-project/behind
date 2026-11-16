using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

public partial class _20261113120000_ContentAgeOptionNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Name", table: "ContentAgeOptions", type: "TEXT", maxLength: 200, nullable: true);
        migrationBuilder.Sql("UPDATE \"ContentAgeOptions\" SET \"Name\" = CAST(\"Age\" AS TEXT)");
        migrationBuilder.AlterColumn<string>(name: "Name", table: "ContentAgeOptions", type: "TEXT", maxLength: 200, nullable: false, oldClrType: typeof(string), oldType: "TEXT", oldMaxLength: 200, oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Name", table: "ContentAgeOptions");
    }
}

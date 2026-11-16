using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

public partial class _20261113120001_ContentAgeOptionNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Name", table: "ContentAgeOptions", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.Sql("UPDATE \"ContentAgeOptions\" SET \"Name\" = CAST(\"Age\" AS text)");
        migrationBuilder.AlterColumn<string>(name: "Name", table: "ContentAgeOptions", type: "character varying(200)", maxLength: 200, nullable: false, oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200, oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Name", table: "ContentAgeOptions");
    }
}

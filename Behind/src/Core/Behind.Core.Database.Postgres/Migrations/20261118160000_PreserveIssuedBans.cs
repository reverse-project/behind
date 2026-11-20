using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Postgres.Migrations;

[DbContext(typeof(CoreContextPostgres))]
[Migration("20261118160000_PreserveIssuedBans")]
public sealed partial class PreserveIssuedBans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_WallOfShameEntries_Users_GivenById", "WallOfShameEntries");
        migrationBuilder.AddForeignKey("FK_WallOfShameEntries_Users_GivenById", "WallOfShameEntries", "GivenById", "Users", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_WallOfShameEntries_Users_GivenById", "WallOfShameEntries");
        migrationBuilder.AddForeignKey("FK_WallOfShameEntries_Users_GivenById", "WallOfShameEntries", "GivenById", "Users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }
}

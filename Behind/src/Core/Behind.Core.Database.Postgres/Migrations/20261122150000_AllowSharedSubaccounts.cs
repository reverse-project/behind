using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Postgres.Migrations;

[DbContext(typeof(CoreContextPostgres))]
[Migration("20261122150000_AllowSharedSubaccounts")]
public sealed partial class AllowSharedSubaccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey("PK_Subaccounts", "Subaccounts");
        migrationBuilder.DropIndex("IX_Subaccounts_PrimaryUserId", "Subaccounts");
        migrationBuilder.AddPrimaryKey("PK_Subaccounts", "Subaccounts", ["PrimaryUserId", "UserId"]);
        migrationBuilder.CreateIndex("IX_Subaccounts_UserId", "Subaccounts", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey("PK_Subaccounts", "Subaccounts");
        migrationBuilder.DropIndex("IX_Subaccounts_UserId", "Subaccounts");
        migrationBuilder.AddPrimaryKey("PK_Subaccounts", "Subaccounts", "UserId");
        migrationBuilder.CreateIndex("IX_Subaccounts_PrimaryUserId", "Subaccounts", "PrimaryUserId");
    }
}

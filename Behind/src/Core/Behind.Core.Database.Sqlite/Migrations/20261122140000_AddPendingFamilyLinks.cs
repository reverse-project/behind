using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite.Migrations;

[DbContext(typeof(CoreContextSqlite))]
[Migration("20261122140000_AddPendingFamilyLinks")]
public sealed partial class AddPendingFamilyLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("PendingParentId", "Users", nullable: true);
        migrationBuilder.AddColumn<Guid>("PendingLinkRequestId", "Users", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("PendingLinkRequestId", "Users");
        migrationBuilder.DropColumn("PendingParentId", "Users");
    }
}

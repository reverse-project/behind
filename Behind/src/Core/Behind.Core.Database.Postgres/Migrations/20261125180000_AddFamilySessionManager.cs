using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Postgres.Migrations;

[DbContext(typeof(CoreContextPostgres))]
[Migration("20261125180000_AddFamilySessionManager")]
public sealed class AddFamilySessionManager : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<Guid>("FamilyManagerUserId", "Sessions", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("FamilyManagerUserId", "Sessions");
}

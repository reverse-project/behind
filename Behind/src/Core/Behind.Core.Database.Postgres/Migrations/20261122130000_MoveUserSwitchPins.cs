using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Postgres.Migrations;

[DbContext(typeof(CoreContextPostgres))]
[Migration("20261122130000_MoveUserSwitchPins")]
public sealed partial class MoveUserSwitchPins : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserSwitchPins",
            columns: table => new
            {
                UserId = table.Column<Guid>(nullable: false),
                PinHash = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSwitchPins", x => x.UserId);
                table.ForeignKey("FK_UserSwitchPins_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.Sql("""
            INSERT INTO "UserSwitchPins" ("UserId", "PinHash")
            SELECT "UserId", "PinHash" FROM "Subaccounts" WHERE "PinHash" IS NOT NULL;
            ALTER TABLE "Subaccounts" DROP COLUMN "PinHash";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("PinHash", "Subaccounts", nullable: true);
        migrationBuilder.Sql("""
            UPDATE "Subaccounts" SET "PinHash" = (
                SELECT p."PinHash" FROM "UserSwitchPins" p WHERE p."UserId" = "Subaccounts"."UserId"
            );
            """);
        migrationBuilder.DropTable("UserSwitchPins");
    }
}

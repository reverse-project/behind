using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite.Migrations;

[DbContext(typeof(CoreContextSqlite))]
[Migration("20261122120000_AddSubaccounts")]
public sealed partial class AddSubaccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("FamilyPrimaryUserId", "Sessions", nullable: true);
        migrationBuilder.CreateTable(
            name: "Subaccounts",
            columns: table => new
            {
                UserId = table.Column<Guid>(nullable: false),
                PrimaryUserId = table.Column<Guid>(nullable: false),
                IsChild = table.Column<bool>(nullable: false),
                PinHash = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subaccounts", x => x.UserId);
                table.CheckConstraint("CK_Subaccounts_DifferentUsers", "\"PrimaryUserId\" <> \"UserId\"");
                table.ForeignKey("FK_Subaccounts_Users_PrimaryUserId", x => x.PrimaryUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Subaccounts_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_Subaccounts_PrimaryUserId", "Subaccounts", "PrimaryUserId");
        migrationBuilder.Sql("""
            CREATE TRIGGER "SubaccountDepthInsert" BEFORE INSERT ON "Subaccounts"
            WHEN EXISTS (SELECT 1 FROM "Subaccounts" WHERE "UserId" = NEW."PrimaryUserId" OR "PrimaryUserId" = NEW."UserId")
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts allow only one level');
            END;
            CREATE TRIGGER "SubaccountDepthUpdate" BEFORE UPDATE OF "PrimaryUserId", "UserId" ON "Subaccounts"
            WHEN EXISTS (SELECT 1 FROM "Subaccounts" WHERE "UserId" = NEW."PrimaryUserId" OR "PrimaryUserId" = NEW."UserId")
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts allow only one level');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("FamilyPrimaryUserId", "Sessions");
        migrationBuilder.DropTable("Subaccounts");
    }
}

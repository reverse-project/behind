using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite.Migrations;

[DbContext(typeof(CoreContextSqlite))]
[Migration("20261122150000_AllowSharedSubaccounts")]
public sealed partial class AllowSharedSubaccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Rebuild(migrationBuilder, allowSharedLinks: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        Rebuild(migrationBuilder, allowSharedLinks: false);
    }

    private static void Rebuild(MigrationBuilder migrationBuilder, bool allowSharedLinks)
    {
        migrationBuilder.CreateTable(
            name: "Subaccounts_New",
            columns: table => new
            {
                PrimaryUserId = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                IsChild = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                if (allowSharedLinks)
                {
                    table.PrimaryKey("PK_Subaccounts", x => new { x.PrimaryUserId, x.UserId });
                }
                else
                {
                    table.PrimaryKey("PK_Subaccounts", x => x.UserId);
                }
                table.CheckConstraint("CK_Subaccounts_DifferentUsers", "\"PrimaryUserId\" <> \"UserId\"");
                table.ForeignKey("FK_Subaccounts_Users_PrimaryUserId", x => x.PrimaryUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Subaccounts_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.Sql("""
            INSERT INTO "Subaccounts_New" ("PrimaryUserId", "UserId", "IsChild")
            SELECT "PrimaryUserId", "UserId", "IsChild" FROM "Subaccounts";
            """);
        migrationBuilder.DropTable("Subaccounts");
        migrationBuilder.RenameTable("Subaccounts_New", newName: "Subaccounts");
        var indexedColumn = allowSharedLinks ? "UserId" : "PrimaryUserId";
        migrationBuilder.CreateIndex($"IX_Subaccounts_{indexedColumn}", "Subaccounts", indexedColumn);
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
}

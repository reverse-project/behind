using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite.Migrations;

[DbContext(typeof(CoreContextSqlite))]
[Migration("20261125193000_RepairSubaccountVersions")]
public sealed class RepairSubaccountVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER "SubaccountDepthUpdate";
            UPDATE "Subaccounts" SET "Key" = upper("Key") WHERE "Key" <> upper("Key");
            CREATE TRIGGER "SubaccountsCanonicalKeyGuard" BEFORE INSERT ON "Subaccounts"
            WHEN NEW."Key" <> upper(NEW."Key")
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts keys must be uppercase');
            END;
            CREATE TRIGGER "SubaccountDepthUpdate" BEFORE UPDATE ON "Subaccounts"
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts versions are append-only');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER "SubaccountsCanonicalKeyGuard";
            DROP TRIGGER "SubaccountDepthUpdate";
            CREATE TRIGGER "SubaccountDepthUpdate" BEFORE UPDATE OF "PrimaryUserId", "UserId", "Key", "Version", "IsActive" ON "Subaccounts"
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts versions are append-only');
            END;
            """);
    }
}

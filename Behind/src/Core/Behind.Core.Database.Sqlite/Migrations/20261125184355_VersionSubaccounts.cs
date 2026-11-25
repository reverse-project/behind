using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite.Migrations;

[DbContext(typeof(CoreContextSqlite))]
[Migration("20261125184355_VersionSubaccounts")]
public sealed partial class _20261125184355_VersionSubaccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS "SubaccountDepthInsert";
            DROP TRIGGER IF EXISTS "SubaccountDepthUpdate";
            CREATE TEMP TABLE "Subaccounts_KeyMap" AS
            SELECT "PrimaryUserId", "UserId",
                   lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' ||
                         hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))) AS "Key",
                   CASE WHEN
                       (SELECT COUNT(*) FROM "AuditLogEntries" a
                        WHERE a."EntityName" = 'Subaccount'
                          AND lower(a."EntityKey") = lower(s."PrimaryUserId" || ':' || s."UserId")
                          AND a."Action" = 'Deleted')
                       >
                       (SELECT COUNT(*) FROM "AuditLogEntries" a
                        WHERE a."EntityName" = 'Subaccount'
                          AND lower(a."EntityKey") = lower(s."PrimaryUserId" || ':' || s."UserId")
                          AND a."Action" = 'Restored')
                   THEN 0 ELSE 1 END AS "IsActive"
            FROM "Subaccounts" s;
            """);

        migrationBuilder.CreateTable(
            name: "Subaccounts_New",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                PrimaryUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                IsChild = table.Column<bool>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subaccounts", row => new { row.Key, row.Version });
                table.CheckConstraint("CK_Subaccounts_DifferentUsers", "\"PrimaryUserId\" <> \"UserId\"");
                table.ForeignKey("FK_Subaccounts_Users_PrimaryUserId", row => row.PrimaryUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Subaccounts_Users_UserId", row => row.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql("""
            INSERT INTO "Subaccounts_New" ("Key", "Version", "PrimaryUserId", "UserId", "IsChild", "IsActive")
            SELECT m."Key", 1, s."PrimaryUserId", s."UserId", s."IsChild", m."IsActive"
            FROM "Subaccounts" s
            JOIN "Subaccounts_KeyMap" m
              ON m."PrimaryUserId" = s."PrimaryUserId" AND m."UserId" = s."UserId";

            UPDATE "AuditLogEntries"
            SET "EntityKey" = (
                SELECT m."Key" || ':1'
                FROM "Subaccounts_KeyMap" m
                WHERE lower(m."PrimaryUserId" || ':' || m."UserId") = lower("AuditLogEntries"."EntityKey")
            )
            WHERE "EntityName" = 'Subaccount'
              AND EXISTS (
                SELECT 1 FROM "Subaccounts_KeyMap" m
                WHERE lower(m."PrimaryUserId" || ':' || m."UserId") = lower("AuditLogEntries"."EntityKey")
              );
            """);

        migrationBuilder.DropTable("Subaccounts");
        migrationBuilder.RenameTable("Subaccounts_New", newName: "Subaccounts");
        migrationBuilder.CreateIndex("IX_Subaccounts_PrimaryUserId", "Subaccounts", "PrimaryUserId");
        migrationBuilder.CreateIndex("IX_Subaccounts_UserId", "Subaccounts", "UserId");
        migrationBuilder.Sql("""
            DROP TABLE "Subaccounts_KeyMap";

            CREATE TRIGGER "SubaccountsVersionGuard" BEFORE INSERT ON "Subaccounts"
            WHEN NEW."Version" != COALESCE(
                (SELECT MAX("Version") FROM "Subaccounts" WHERE lower("Key") = lower(NEW."Key")), 0) + 1
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts version conflict');
            END;

            CREATE TRIGGER "SubaccountsPairIdentityGuard" BEFORE INSERT ON "Subaccounts"
            WHEN EXISTS (
                SELECT 1 FROM "Subaccounts"
                WHERE "PrimaryUserId" = NEW."PrimaryUserId" AND "UserId" = NEW."UserId"
                  AND lower("Key") <> lower(NEW."Key")
            )
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts pair identity conflict');
            END;

            CREATE TRIGGER "SubaccountsKeyIdentityGuard" BEFORE INSERT ON "Subaccounts"
            WHEN EXISTS (
                SELECT 1 FROM "Subaccounts"
                WHERE lower("Key") = lower(NEW."Key")
                  AND ("PrimaryUserId" <> NEW."PrimaryUserId" OR "UserId" <> NEW."UserId")
            )
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts key identity conflict');
            END;

            CREATE TRIGGER "SubaccountDepthInsert" BEFORE INSERT ON "Subaccounts"
            WHEN NEW."IsActive" = 1 AND EXISTS (
                SELECT 1 FROM "Subaccounts" existing
                WHERE lower(existing."Key") <> lower(NEW."Key")
                  AND existing."Version" = (
                    SELECT MAX(latest."Version") FROM "Subaccounts" latest WHERE latest."Key" = existing."Key"
                  )
                  AND existing."IsActive" = 1
                  AND (existing."UserId" = NEW."PrimaryUserId" OR existing."PrimaryUserId" = NEW."UserId")
            )
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts allow only one level');
            END;

            CREATE TRIGGER "SubaccountDepthUpdate" BEFORE UPDATE OF "PrimaryUserId", "UserId", "Key", "Version", "IsActive" ON "Subaccounts"
            BEGIN
                SELECT RAISE(ABORT, 'Subaccounts versions are append-only');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS "SubaccountsVersionGuard";
            DROP TRIGGER IF EXISTS "SubaccountsPairIdentityGuard";
            DROP TRIGGER IF EXISTS "SubaccountsKeyIdentityGuard";
            DROP TRIGGER IF EXISTS "SubaccountDepthInsert";
            DROP TRIGGER IF EXISTS "SubaccountDepthUpdate";
            CREATE TEMP TABLE "Subaccounts_KeyMap" AS
            SELECT s."Key", s."PrimaryUserId", s."UserId", s."IsChild", s."IsActive"
            FROM "Subaccounts" s
            WHERE s."Version" = (
                SELECT MAX(latest."Version") FROM "Subaccounts" latest WHERE latest."Key" = s."Key"
            );

            UPDATE "AuditLogEntries"
            SET "EntityKey" = (
                SELECT lower(m."PrimaryUserId") || ':' || lower(m."UserId")
                FROM "Subaccounts_KeyMap" m
                WHERE lower("AuditLogEntries"."EntityKey") LIKE lower(m."Key") || ':%'
            )
            WHERE "EntityName" = 'Subaccount'
              AND EXISTS (
                SELECT 1 FROM "Subaccounts_KeyMap" m
                WHERE lower("AuditLogEntries"."EntityKey") LIKE lower(m."Key") || ':%'
              );

            INSERT INTO "AuditLogEntries" ("Id", "EntityName", "EntityKey", "Action", "Timestamp", "UserId", "BehalfOfId")
            SELECT lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' ||
                         hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))),
                   'Subaccount', lower(m."PrimaryUserId") || ':' || lower(m."UserId"),
                   CASE WHEN m."IsActive" = 1 THEN 'Restored' ELSE 'Deleted' END,
                   CAST(strftime('%s', 'now') AS INTEGER) * 10000000 + 621355968000000000, NULL, NULL
            FROM "Subaccounts_KeyMap" m
            WHERE (m."IsActive" = 0 AND
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId") || ':' || lower(m."UserId")
                      AND a."Action" = 'Deleted')
                   <=
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId") || ':' || lower(m."UserId")
                      AND a."Action" = 'Restored'))
               OR (m."IsActive" = 1 AND
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId") || ':' || lower(m."UserId")
                      AND a."Action" = 'Deleted')
                   >
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId") || ':' || lower(m."UserId")
                      AND a."Action" = 'Restored'));
            """);

        migrationBuilder.CreateTable(
            name: "Subaccounts_Old",
            columns: table => new
            {
                PrimaryUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                IsChild = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subaccounts", row => new { row.PrimaryUserId, row.UserId });
                table.CheckConstraint("CK_Subaccounts_DifferentUsers", "\"PrimaryUserId\" <> \"UserId\"");
                table.ForeignKey("FK_Subaccounts_Users_PrimaryUserId", row => row.PrimaryUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Subaccounts_Users_UserId", row => row.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql("""
            INSERT INTO "Subaccounts_Old" ("PrimaryUserId", "UserId", "IsChild")
            SELECT "PrimaryUserId", "UserId", "IsChild"
            FROM "Subaccounts_KeyMap";
            """);

        migrationBuilder.DropTable("Subaccounts");
        migrationBuilder.RenameTable("Subaccounts_Old", newName: "Subaccounts");
        migrationBuilder.CreateIndex("IX_Subaccounts_UserId", "Subaccounts", "UserId");
        migrationBuilder.Sql("""
            DROP TABLE "Subaccounts_KeyMap";

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

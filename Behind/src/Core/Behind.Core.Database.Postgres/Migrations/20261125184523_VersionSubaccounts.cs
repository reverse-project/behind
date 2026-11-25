using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Postgres.Migrations;

[DbContext(typeof(CoreContextPostgres))]
[Migration("20261125184523_VersionSubaccounts")]
public sealed partial class _20261125184523_VersionSubaccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER "SubaccountDepth" ON "Subaccounts";
            DROP FUNCTION behind_check_subaccount_depth();

            CREATE TEMP TABLE "Subaccounts_KeyMap" ON COMMIT DROP AS
            SELECT s."PrimaryUserId", s."UserId", gen_random_uuid() AS "Key",
                   NOT (
                       (SELECT COUNT(*) FROM "AuditLogEntries" a
                        WHERE a."EntityName" = 'Subaccount'
                          AND lower(a."EntityKey") = lower(s."PrimaryUserId"::text || ':' || s."UserId"::text)
                          AND a."Action" = 'Deleted')
                       >
                       (SELECT COUNT(*) FROM "AuditLogEntries" a
                        WHERE a."EntityName" = 'Subaccount'
                          AND lower(a."EntityKey") = lower(s."PrimaryUserId"::text || ':' || s."UserId"::text)
                          AND a."Action" = 'Restored')
                   ) AS "IsActive"
            FROM "Subaccounts" s;
            """);

        migrationBuilder.CreateTable(
            name: "Subaccounts_New",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                PrimaryUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                IsChild = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subaccounts_New", row => new { row.Key, row.Version });
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

            UPDATE "AuditLogEntries" a
            SET "EntityKey" = m."Key"::text || ':1'
            FROM "Subaccounts_KeyMap" m
            WHERE a."EntityName" = 'Subaccount'
              AND lower(a."EntityKey") = lower(m."PrimaryUserId"::text || ':' || m."UserId"::text);
            """);

        migrationBuilder.DropTable("Subaccounts");
        migrationBuilder.RenameTable(name: "Subaccounts_New", newName: "Subaccounts");
        migrationBuilder.Sql(
            """ALTER TABLE "Subaccounts" RENAME CONSTRAINT "PK_Subaccounts_New" TO "PK_Subaccounts";""");
        migrationBuilder.CreateIndex("IX_Subaccounts_PrimaryUserId", "Subaccounts", "PrimaryUserId");
        migrationBuilder.CreateIndex("IX_Subaccounts_UserId", "Subaccounts", "UserId");
        migrationBuilder.Sql("""
            CREATE FUNCTION behind_check_subaccount_version() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE
                expected_version bigint;
            BEGIN
                SELECT COALESCE(MAX("Version"), 0) + 1
                INTO expected_version
                FROM "Subaccounts" WHERE "Key" = NEW."Key";
                IF NEW."Version" <> expected_version THEN
                    RAISE EXCEPTION 'Subaccounts versions must be sequential';
                END IF;

                IF EXISTS (
                    SELECT 1 FROM "Subaccounts"
                    WHERE "PrimaryUserId" = NEW."PrimaryUserId" AND "UserId" = NEW."UserId"
                      AND "Key" <> NEW."Key"
                ) OR EXISTS (
                    SELECT 1 FROM "Subaccounts"
                    WHERE "Key" = NEW."Key"
                      AND ("PrimaryUserId" <> NEW."PrimaryUserId" OR "UserId" <> NEW."UserId")
                ) THEN
                    RAISE EXCEPTION 'Subaccounts link identity conflict';
                END IF;

                PERFORM "Id" FROM "Users"
                WHERE "Id" IN (NEW."PrimaryUserId", NEW."UserId")
                ORDER BY "Id" FOR UPDATE;

                IF NEW."IsActive" AND EXISTS (
                    SELECT 1 FROM "Subaccounts" existing
                    WHERE existing."Version" = (
                        SELECT MAX(latest."Version")
                        FROM "Subaccounts" latest WHERE latest."Key" = existing."Key"
                    )
                      AND existing."IsActive"
                      AND (existing."UserId" = NEW."PrimaryUserId"
                           OR existing."PrimaryUserId" = NEW."UserId")
                ) THEN
                    RAISE EXCEPTION 'Subaccounts allow only one level';
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER "SubaccountsVersionGuard" BEFORE INSERT ON "Subaccounts"
            FOR EACH ROW EXECUTE FUNCTION behind_check_subaccount_version();

            CREATE FUNCTION behind_prevent_subaccount_update() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Subaccounts versions are append-only';
            END;
            $$;

            CREATE TRIGGER "SubaccountsAppendOnly" BEFORE UPDATE ON "Subaccounts"
            FOR EACH ROW EXECUTE FUNCTION behind_prevent_subaccount_update();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER "SubaccountsVersionGuard" ON "Subaccounts";
            DROP FUNCTION behind_check_subaccount_version();
            DROP TRIGGER "SubaccountsAppendOnly" ON "Subaccounts";
            DROP FUNCTION behind_prevent_subaccount_update();

            CREATE TEMP TABLE "Subaccounts_KeyMap" ON COMMIT DROP AS
            SELECT s."Key", s."PrimaryUserId", s."UserId", s."IsChild", s."IsActive"
            FROM "Subaccounts" s
            WHERE s."Version" = (
                SELECT MAX(latest."Version") FROM "Subaccounts" latest WHERE latest."Key" = s."Key"
            );

            UPDATE "AuditLogEntries" a
            SET "EntityKey" = lower(m."PrimaryUserId"::text) || ':' || lower(m."UserId"::text)
            FROM "Subaccounts_KeyMap" m
            WHERE a."EntityName" = 'Subaccount'
              AND lower(a."EntityKey") LIKE lower(m."Key"::text) || ':%';

            INSERT INTO "AuditLogEntries" ("Id", "EntityName", "EntityKey", "Action", "Timestamp", "UserId", "BehalfOfId")
            SELECT gen_random_uuid(), 'Subaccount',
                   lower(m."PrimaryUserId"::text) || ':' || lower(m."UserId"::text),
                   CASE WHEN m."IsActive" THEN 'Restored' ELSE 'Deleted' END,
                   (EXTRACT(EPOCH FROM clock_timestamp()) * 10000000)::bigint + 621355968000000000,
                   NULL, NULL
            FROM "Subaccounts_KeyMap" m
            WHERE (NOT m."IsActive" AND
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId"::text) || ':' || lower(m."UserId"::text)
                      AND a."Action" = 'Deleted')
                   <=
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId"::text) || ':' || lower(m."UserId"::text)
                      AND a."Action" = 'Restored'))
               OR (m."IsActive" AND
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId"::text) || ':' || lower(m."UserId"::text)
                      AND a."Action" = 'Deleted')
                   >
                   (SELECT COUNT(*) FROM "AuditLogEntries" a
                    WHERE a."EntityName" = 'Subaccount'
                      AND lower(a."EntityKey") = lower(m."PrimaryUserId"::text) || ':' || lower(m."UserId"::text)
                      AND a."Action" = 'Restored'));
            """);

        migrationBuilder.CreateTable(
            name: "Subaccounts_Old",
            columns: table => new
            {
                PrimaryUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                IsChild = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subaccounts_Old", row => new { row.PrimaryUserId, row.UserId });
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
        migrationBuilder.RenameTable(name: "Subaccounts_Old", newName: "Subaccounts");
        migrationBuilder.Sql(
            """ALTER TABLE "Subaccounts" RENAME CONSTRAINT "PK_Subaccounts_Old" TO "PK_Subaccounts";""");
        migrationBuilder.CreateIndex("IX_Subaccounts_UserId", "Subaccounts", "UserId");
        migrationBuilder.Sql("""
            CREATE FUNCTION behind_check_subaccount_depth() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                PERFORM "Id" FROM "Users" WHERE "Id" IN (NEW."PrimaryUserId", NEW."UserId") ORDER BY "Id" FOR UPDATE;
                IF EXISTS (SELECT 1 FROM "Subaccounts" WHERE "UserId" = NEW."PrimaryUserId" OR "PrimaryUserId" = NEW."UserId") THEN
                    RAISE EXCEPTION 'Subaccounts allow only one level';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER "SubaccountDepth" BEFORE INSERT OR UPDATE OF "PrimaryUserId", "UserId" ON "Subaccounts"
            FOR EACH ROW EXECUTE FUNCTION behind_check_subaccount_depth();
            """);
    }
}

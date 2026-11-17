namespace Behind.Core.Database;

public static class RoleNameConflictMigration
{
    public const string PostgresPreflightSql = """
        DO $preflight$
        DECLARE conflicts text;
        BEGIN
            SELECT string_agg(format('%s: %s', names."NormalizedName", names.ids), '; ')
            INTO conflicts
            FROM (
                SELECT "NormalizedName", string_agg("Id"::text, ', ' ORDER BY "Id") AS ids
                FROM "Roles"
                GROUP BY "NormalizedName"
                HAVING count(*) > 1
            ) names;
            IF conflicts IS NOT NULL THEN
                RAISE EXCEPTION 'Role name conflicts prevent UX_Roles_NormalizedName creation: %. Rename conflicting roles explicitly, preserving their IDs, permissions and references, then retry.', conflicts;
            END IF;
        END $preflight$;
        """;

    public const string SqlitePreflightSql = """
        CREATE TEMP TABLE "BehindRoleNamePreflight" ("Run" INTEGER);
        CREATE TEMP TRIGGER "BehindRoleNamePreflightCheck"
        BEFORE INSERT ON "BehindRoleNamePreflight"
        WHEN EXISTS (SELECT 1 FROM "Roles" GROUP BY "NormalizedName" HAVING count(*) > 1)
        BEGIN
            SELECT RAISE(ABORT, 'Role name conflicts prevent UX_Roles_NormalizedName creation: ' ||
                (SELECT group_concat(details, '; ') FROM (
                    SELECT "NormalizedName" || ': ' || group_concat("Id", ', ') AS details
                    FROM "Roles" GROUP BY "NormalizedName" HAVING count(*) > 1
                )) || '. Rename conflicting roles explicitly, preserving their IDs, permissions and references, then retry.');
        END;
        INSERT INTO "BehindRoleNamePreflight" VALUES (1);
        DROP TRIGGER "BehindRoleNamePreflightCheck";
        DROP TABLE "BehindRoleNamePreflight";
        """;
}

using Behind.Common;

namespace Behind.Core.Database;

public static class RoleNameKeyMigration
{
    public static string CreatePostgresFunctionSql()
    {
        var source = string.Concat(OrdinalIgnoreCaseKey.Mappings.Select(pair =>
            pair.Source)).Replace("'", "''", StringComparison.Ordinal);
        var target = string.Concat(OrdinalIgnoreCaseKey.Mappings.Select(pair =>
            pair.Target)).Replace("'", "''", StringComparison.Ordinal);
        return $$"""
            CREATE FUNCTION "BehindOrdinalIgnoreCaseKey"(value text) RETURNS text
            LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
            AS $function$
                SELECT translate(value, '{{source}}', '{{target}}')
            $function$;
            """;
    }
}

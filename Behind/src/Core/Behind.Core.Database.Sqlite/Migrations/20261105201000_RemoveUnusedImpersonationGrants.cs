using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedImpersonationGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "RoleGrants" WHERE "GrantId" IN (SELECT "Id" FROM "Grants" WHERE "Name" IN ('Impersonation.Read', 'Impersonation.Write', 'Impersonation.Delete'));
                DELETE FROM "UserGrants" WHERE "GrantId" IN (SELECT "Id" FROM "Grants" WHERE "Name" IN ('Impersonation.Read', 'Impersonation.Write', 'Impersonation.Delete'));
                DELETE FROM "RoleRestrictions" WHERE "RestrictionId" IN (SELECT "Id" FROM "Restrictions" WHERE "Name" IN ('Impersonation.Read', 'Impersonation.Write', 'Impersonation.Delete'));
                DELETE FROM "UserRestrictions" WHERE "RestrictionId" IN (SELECT "Id" FROM "Restrictions" WHERE "Name" IN ('Impersonation.Read', 'Impersonation.Write', 'Impersonation.Delete'));
                DELETE FROM "Grants" WHERE "Name" IN ('Impersonation.Read', 'Impersonation.Write', 'Impersonation.Delete');
                DELETE FROM "Restrictions" WHERE "Name" IN ('Impersonation.Read', 'Impersonation.Write', 'Impersonation.Delete');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

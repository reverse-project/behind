using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class GrantDevelopersConfigurationManage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "RoleGrants" ("RoleId", "GrantId")
                SELECT '50000000-0000-0000-0000-000000000000', g."Id"
                FROM "Grants" g
                WHERE g."Name" = 'Configuration.Manage'
                  AND EXISTS (SELECT 1 FROM "Roles" r WHERE r."Id" = '50000000-0000-0000-0000-000000000000')
                  AND NOT EXISTS (
                      SELECT 1 FROM "RoleGrants" rg
                      WHERE rg."RoleId" = '50000000-0000-0000-0000-000000000000' AND rg."GrantId" = g."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

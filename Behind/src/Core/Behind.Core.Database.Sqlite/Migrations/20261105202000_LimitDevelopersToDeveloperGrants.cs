using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class LimitDevelopersToDeveloperGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "RoleGrants"
                WHERE "RoleId" = '50000000-0000-0000-0000-000000000000'
                  AND "GrantId" NOT IN (
                      SELECT "Id" FROM "Grants"
                      WHERE "Name" IN ('Configuration.Read', 'Configuration.Manage', 'RawDatabase.Manage'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeveloper",
                table: "Roles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE "Roles" SET "IsDeveloper" = 1
                WHERE "Id" = '50000000-0000-0000-0000-000000000000'
                   OR ("Id" NOT IN ('30000000-0000-0000-0000-000000000000', '40000000-0000-0000-0000-000000000000') AND "Id" IN (
                       SELECT rg."RoleId" FROM "RoleGrants" rg
                       JOIN "Grants" g ON g."Id" = rg."GrantId"
                       WHERE g."Name" LIKE 'Configuration.%' OR g."Name" = 'RawDatabase.Manage'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeveloper",
                table: "Roles");
        }
    }
}

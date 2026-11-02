using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations.Versioning
{
    /// <inheritdoc />
    public partial class PublishExistingNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "INSERT OR IGNORE INTO \"ActiveVersions\" (\"EntityType\", \"EntityKey\", \"Version\") "
                + "SELECT 'News', lower(\"Key\"), MAX(\"Version\") FROM \"News\" GROUP BY \"Key\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

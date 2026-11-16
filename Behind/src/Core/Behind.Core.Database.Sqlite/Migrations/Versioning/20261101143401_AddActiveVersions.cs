using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations.Versioning
{
    /// <inheritdoc />
    public partial class AddActiveVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveVersions",
                columns: table => new
                {
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveVersions", x => new { x.EntityType, x.EntityKey });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveVersions");
        }
    }
}

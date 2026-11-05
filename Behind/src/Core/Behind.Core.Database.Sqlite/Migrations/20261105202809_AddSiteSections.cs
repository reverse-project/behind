using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteSections",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ImageAlt = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ImagePosition = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSections", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteSections_ImageId",
                table: "SiteSections",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSections_SiteId_Version",
                table: "SiteSections",
                columns: new[] { "SiteId", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSections");
        }
    }
}

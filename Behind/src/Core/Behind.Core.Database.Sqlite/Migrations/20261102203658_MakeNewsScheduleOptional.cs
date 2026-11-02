using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MakeNewsScheduleOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StartAtUtc",
                table: "News",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"News\" SET \"StartAtUtc\" = \"StartAt\" "
                + "WHERE \"EndAtUtc\" IS NOT NULL "
                + "OR \"StartAt\" > (CAST(strftime('%s', 'now') AS INTEGER) * 10000000 + 621355968000000000);");

            migrationBuilder.DropIndex(
                name: "IX_News_StartAt",
                table: "News");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "News");

            migrationBuilder.CreateIndex(
                name: "IX_News_StartAtUtc",
                table: "News",
                column: "StartAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StartAt",
                table: "News",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("UPDATE \"News\" SET \"StartAt\" = \"StartAtUtc\" WHERE \"StartAtUtc\" IS NOT NULL;");

            migrationBuilder.DropIndex(
                name: "IX_News_StartAtUtc",
                table: "News");

            migrationBuilder.DropColumn(
                name: "StartAtUtc",
                table: "News");

            migrationBuilder.CreateIndex(
                name: "IX_News_StartAt",
                table: "News",
                column: "StartAt");
        }
    }
}

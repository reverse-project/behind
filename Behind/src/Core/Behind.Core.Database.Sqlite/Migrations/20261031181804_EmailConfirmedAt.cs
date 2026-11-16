using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class EmailConfirmedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ConfirmedAt",
                table: "Emails",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Emails SET ConfirmedAt = CAST(strftime('%s', 'now') AS INTEGER) * 10000000 + 621355968000000000 WHERE IsConfirmed = 1;");

            migrationBuilder.DropColumn(
                name: "IsConfirmed",
                table: "Emails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmed",
                table: "Emails",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Emails SET IsConfirmed = 1 WHERE ConfirmedAt IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Emails");
        }
    }
}

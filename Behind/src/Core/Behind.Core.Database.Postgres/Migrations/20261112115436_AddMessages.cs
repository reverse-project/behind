using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261112115436_AddMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                StartsAt = table.Column<long>(type: "bigint", nullable: true),
                EndsAt = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Messages", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_Name",
            table: "Messages",
            column: "Name");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Messages");
    }
}

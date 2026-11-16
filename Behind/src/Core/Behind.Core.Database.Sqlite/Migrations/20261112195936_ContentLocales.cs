using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

public partial class _20261112195936_ContentLocales : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContentLocales",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                EntityType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                Locale = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentLocales", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ContentLocales_EntityType_EntityId_Locale",
            table: "ContentLocales",
            columns: new[] { "EntityType", "EntityId", "Locale" });
        migrationBuilder.CreateIndex(
            name: "IX_ContentLocales_EntityType_EntityId_Version",
            table: "ContentLocales",
            columns: new[] { "EntityType", "EntityId", "Version" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ContentLocales");
    }
}

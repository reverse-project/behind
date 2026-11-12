using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

public partial class _20261112195911_ContentLocales : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContentLocales",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                EntityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                Locale = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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

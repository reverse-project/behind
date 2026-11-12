using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

public partial class _20261112082917_ProfilesPrivacyAndContentRestrictions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "Birthday",
            table: "Users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "HideEmail",
            table: "Users",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymous",
            table: "NewsComments",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymous",
            table: "ChatMessages",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "ContentRestrictions",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                EntityType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                MinimumAge = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentRestrictions", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ContentRestrictions_EntityType_EntityId",
            table: "ContentRestrictions",
            columns: new[] { "EntityType", "EntityId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ContentRestrictions");

        migrationBuilder.DropColumn(
            name: "Birthday",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "HideEmail",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "IsAnonymous",
            table: "NewsComments");

        migrationBuilder.DropColumn(
            name: "IsAnonymous",
            table: "ChatMessages");
    }
}

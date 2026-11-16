using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

public partial class _20261112082920_ProfilesPrivacyAndContentRestrictions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "Birthday",
            table: "Users",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "HideEmail",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymous",
            table: "NewsComments",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymous",
            table: "ChatMessages",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "ContentRestrictions",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                EntityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                MinimumAge = table.Column<int>(type: "integer", nullable: false)
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

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Forum.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261111193450_AddBoardCategories : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CategoryId",
            table: "ForumBoards",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ForumBoardCategories",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForumBoardCategories", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ForumBoards_CategoryId",
            table: "ForumBoards",
            column: "CategoryId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ForumBoardCategories");

        migrationBuilder.DropIndex(
            name: "IX_ForumBoards_CategoryId",
            table: "ForumBoards");

        migrationBuilder.DropColumn(
            name: "CategoryId",
            table: "ForumBoards");
    }
}

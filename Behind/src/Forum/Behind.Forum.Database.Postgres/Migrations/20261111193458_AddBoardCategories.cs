using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Forum.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261111193458_AddBoardCategories : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CategoryId",
            table: "ForumBoards",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ForumBoardCategories",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
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

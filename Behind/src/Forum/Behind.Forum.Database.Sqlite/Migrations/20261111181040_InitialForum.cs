using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Forum.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261111181040_InitialForum : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ForumBoards",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                Moderation = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForumBoards", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "ForumPosts",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                TopicId = table.Column<Guid>(type: "TEXT", nullable: true),
                AuthorId = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Body = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                State = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForumPosts", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "ForumUserModerations",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                Moderation = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForumUserModerations", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ForumPosts_BoardId",
            table: "ForumPosts",
            column: "BoardId");

        migrationBuilder.CreateIndex(
            name: "IX_ForumPosts_State",
            table: "ForumPosts",
            column: "State");

        migrationBuilder.CreateIndex(
            name: "IX_ForumPosts_TopicId",
            table: "ForumPosts",
            column: "TopicId");

        migrationBuilder.CreateIndex(
            name: "IX_ForumUserModerations_UserId",
            table: "ForumUserModerations",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ForumBoards");

        migrationBuilder.DropTable(
            name: "ForumPosts");

        migrationBuilder.DropTable(
            name: "ForumUserModerations");
    }
}

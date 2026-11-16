using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Forum.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261111181059_InitialForum : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ForumBoards",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Moderation = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForumBoards", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "ForumPosts",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                TopicId = table.Column<Guid>(type: "uuid", nullable: true),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                State = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForumPosts", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "ForumUserModerations",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Moderation = table.Column<int>(type: "integer", nullable: false)
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

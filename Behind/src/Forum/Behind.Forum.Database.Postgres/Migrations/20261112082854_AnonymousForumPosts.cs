using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Forum.Database.Postgres.Migrations;

public partial class _20261112082854_AnonymousForumPosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymous",
            table: "ForumPosts",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsAnonymous",
            table: "ForumPosts");
    }
}

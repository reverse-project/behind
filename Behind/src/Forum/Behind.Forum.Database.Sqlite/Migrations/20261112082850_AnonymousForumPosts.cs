using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Forum.Database.Sqlite.Migrations;

public partial class _20261112082850_AnonymousForumPosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymous",
            table: "ForumPosts",
            type: "INTEGER",
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

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261111202903_AddChatMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                AuthorId = table.Column<Guid>(type: "TEXT", nullable: false),
                Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                ReplyToId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_AuthorId",
            table: "ChatMessages",
            column: "AuthorId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_ReplyToId",
            table: "ChatMessages",
            column: "ReplyToId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ChatMessages");
    }
}

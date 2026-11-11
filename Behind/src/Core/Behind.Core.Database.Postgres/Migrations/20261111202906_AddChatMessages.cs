using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261111202906_AddChatMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                ReplyToId = table.Column<Guid>(type: "uuid", nullable: true)
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

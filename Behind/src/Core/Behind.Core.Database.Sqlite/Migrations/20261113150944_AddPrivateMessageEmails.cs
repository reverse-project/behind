using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261113150944_AddPrivateMessageEmails : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PrivateMessageEmailDeliveries",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Attempts = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PrivateMessageEmailDeliveries", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessageEmailDeliveries_MessageId",
            table: "PrivateMessageEmailDeliveries",
            column: "MessageId");

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessageEmailDeliveries_Status",
            table: "PrivateMessageEmailDeliveries",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PrivateMessageEmailDeliveries");
    }
}

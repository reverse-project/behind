using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261113150946_AddPrivateMessageEmails : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PrivateMessageEmailDeliveries",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false)
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

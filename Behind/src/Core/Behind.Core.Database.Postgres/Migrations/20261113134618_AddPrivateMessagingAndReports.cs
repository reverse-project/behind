using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261113134618_AddPrivateMessagingAndReports : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PrivateConversationRecipients",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                IsRole = table.Column<bool>(type: "boolean", nullable: false),
                IsHidden = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PrivateConversationRecipients", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "PrivateConversations",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                IsReport = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PrivateConversations", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "PrivateMailboxes",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Deleted = table.Column<bool>(type: "boolean", nullable: false),
                Left = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PrivateMailboxes", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "PrivateMessageReads",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PrivateMessageReads", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "PrivateMessages",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                Content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                ReplyToId = table.Column<Guid>(type: "uuid", nullable: true),
                PrivateRecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                IsInitial = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PrivateMessages", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "PrivateUserBlocks",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PrivateUserBlocks", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "ReportEmailDeliveries",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReportEmailDeliveries", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "UserReports",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetIds = table.Column<string>(type: "character varying(740)", maxLength: 740, nullable: false),
                Reason = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                EvidenceContent = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                Resolution = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ReporterMessage = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                ReportedEmail = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserReports", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_PrivateConversationRecipients_ConversationId_RecipientId_Is~",
            table: "PrivateConversationRecipients",
            columns: new[] { "ConversationId", "RecipientId", "IsRole", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PrivateConversationRecipients_RecipientId",
            table: "PrivateConversationRecipients",
            column: "RecipientId");

        migrationBuilder.CreateIndex(
            name: "IX_PrivateConversations_SenderId",
            table: "PrivateConversations",
            column: "SenderId");

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMailboxes_ConversationId_UserId_Version",
            table: "PrivateMailboxes",
            columns: new[] { "ConversationId", "UserId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessageReads_MessageId_UserId_Version",
            table: "PrivateMessageReads",
            columns: new[] { "MessageId", "UserId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessages_ConversationId",
            table: "PrivateMessages",
            column: "ConversationId");

        migrationBuilder.CreateIndex(
            name: "IX_PrivateMessages_ReplyToId",
            table: "PrivateMessages",
            column: "ReplyToId");

        migrationBuilder.CreateIndex(
            name: "IX_PrivateUserBlocks_UserId_BlockedUserId_Version",
            table: "PrivateUserBlocks",
            columns: new[] { "UserId", "BlockedUserId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReportEmailDeliveries_ReportId",
            table: "ReportEmailDeliveries",
            column: "ReportId");

        migrationBuilder.CreateIndex(
            name: "IX_UserReports_ConversationId",
            table: "UserReports",
            column: "ConversationId");

        migrationBuilder.CreateIndex(
            name: "IX_UserReports_ReporterId",
            table: "UserReports",
            column: "ReporterId");

        migrationBuilder.CreateIndex(
            name: "IX_UserReports_Status",
            table: "UserReports",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PrivateConversationRecipients");

        migrationBuilder.DropTable(
            name: "PrivateConversations");

        migrationBuilder.DropTable(
            name: "PrivateMailboxes");

        migrationBuilder.DropTable(
            name: "PrivateMessageReads");

        migrationBuilder.DropTable(
            name: "PrivateMessages");

        migrationBuilder.DropTable(
            name: "PrivateUserBlocks");

        migrationBuilder.DropTable(
            name: "ReportEmailDeliveries");

        migrationBuilder.DropTable(
            name: "UserReports");
    }
}

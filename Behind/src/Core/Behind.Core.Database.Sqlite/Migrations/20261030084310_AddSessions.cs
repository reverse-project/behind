using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAgents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RemoteAddress = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    InvalidatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    InvalidatedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserAgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessToken_ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    AccessToken_IssuedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    AccessToken_Nonce = table.Column<Guid>(type: "TEXT", nullable: false),
                    RefreshToken_ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RefreshToken_IssuedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RefreshToken_Nonce = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_UserAgents_UserAgentId",
                        column: x => x.UserAgentId,
                        principalTable: "UserAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_InvalidatedById",
                        column: x => x.InvalidatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_InvalidatedAt",
                table: "Sessions",
                column: "InvalidatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_InvalidatedById",
                table: "Sessions",
                column: "InvalidatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserAgentId",
                table: "Sessions",
                column: "UserAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                table: "Sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAgents_Value",
                table: "UserAgents",
                column: "Value",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "UserAgents");
        }
    }
}

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class FixRelations2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Emails_Users_HumanUserId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_WallOfShameEntries_Users_GivenById",
                table: "WallOfShameEntries");

            migrationBuilder.DropIndex(
                name: "IX_WallOfShameEntries_GivenById",
                table: "WallOfShameEntries");

            migrationBuilder.DropIndex(
                name: "IX_Emails_HumanUserId",
                table: "Emails");

            migrationBuilder.DropColumn(
                name: "HumanUserId",
                table: "Emails");

            migrationBuilder.AddColumn<Guid>(
                name: "GivenById",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GivenById",
                table: "Users",
                column: "GivenById");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_WallOfShameEntries_GivenById",
                table: "Users",
                column: "GivenById",
                principalTable: "WallOfShameEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_WallOfShameEntries_GivenById",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GivenById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GivenById",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "HumanUserId",
                table: "Emails",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WallOfShameEntries_GivenById",
                table: "WallOfShameEntries",
                column: "GivenById");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_HumanUserId",
                table: "Emails",
                column: "HumanUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_Users_HumanUserId",
                table: "Emails",
                column: "HumanUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WallOfShameEntries_Users_GivenById",
                table: "WallOfShameEntries",
                column: "GivenById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

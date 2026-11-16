using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class FixRelations3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_WallOfShameEntries_GivenById",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_WallOfShameEntries_UserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GivenById",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GivenById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_WallOfShameEntries_GivenById",
                table: "WallOfShameEntries",
                column: "GivenById");

            migrationBuilder.CreateIndex(
                name: "IX_WallOfShameEntries_UserId",
                table: "WallOfShameEntries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WallOfShameEntries_Users_GivenById",
                table: "WallOfShameEntries",
                column: "GivenById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WallOfShameEntries_Users_UserId",
                table: "WallOfShameEntries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WallOfShameEntries_Users_GivenById",
                table: "WallOfShameEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_WallOfShameEntries_Users_UserId",
                table: "WallOfShameEntries");

            migrationBuilder.DropIndex(
                name: "IX_WallOfShameEntries_GivenById",
                table: "WallOfShameEntries");

            migrationBuilder.DropIndex(
                name: "IX_WallOfShameEntries_UserId",
                table: "WallOfShameEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "GivenById",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GivenById",
                table: "Users",
                column: "GivenById");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserId",
                table: "Users",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_WallOfShameEntries_GivenById",
                table: "Users",
                column: "GivenById",
                principalTable: "WallOfShameEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_WallOfShameEntries_UserId",
                table: "Users",
                column: "UserId",
                principalTable: "WallOfShameEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

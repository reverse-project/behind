using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UserAddDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WallOfShameEntries_GivenById",
                table: "WallOfShameEntries",
                column: "GivenById");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DisplayName",
                table: "Users",
                column: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WallOfShameEntries_GivenById",
                table: "WallOfShameEntries");

            migrationBuilder.DropIndex(
                name: "IX_Users_DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

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
    }
}

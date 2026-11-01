using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionImpersonation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImpersonatorId",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ImpersonatorId",
                table: "Sessions",
                column: "ImpersonatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Users_ImpersonatorId",
                table: "Sessions",
                column: "ImpersonatorId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Users_ImpersonatorId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ImpersonatorId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ImpersonatorId",
                table: "Sessions");
        }
    }
}

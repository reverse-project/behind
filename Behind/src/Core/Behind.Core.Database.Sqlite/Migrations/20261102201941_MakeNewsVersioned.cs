using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MakeNewsVersioned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_News_NewsCategories_CategoryId",
                table: "News");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsComments_News_NewsId",
                table: "NewsComments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NewsComments",
                table: "NewsComments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NewsCategories",
                table: "NewsCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_News",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_Highlighted",
                table: "News");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "NewsComments",
                newName: "Key");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "NewsCategories",
                newName: "Key");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "News",
                newName: "Key");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "NewsComments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "NewsCategories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "News",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_NewsComments",
                table: "NewsComments",
                columns: new[] { "Key", "Version" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_NewsCategories",
                table: "NewsCategories",
                columns: new[] { "Key", "Version" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_News",
                table: "News",
                columns: new[] { "Key", "Version" });

            migrationBuilder.Sql("UPDATE \"News\" SET \"Version\" = 1 WHERE \"Version\" = 0;");
            migrationBuilder.Sql("UPDATE \"NewsCategories\" SET \"Version\" = 1 WHERE \"Version\" = 0;");
            migrationBuilder.Sql("UPDATE \"NewsComments\" SET \"Version\" = 1 WHERE \"Version\" = 0;");
            migrationBuilder.Sql(
                "UPDATE \"AuditLogEntries\" SET \"EntityKey\" = \"EntityKey\" || ':1' "
                + "WHERE \"EntityName\" IN ('News', 'NewsCategory', 'NewsComment') AND \"EntityKey\" NOT LIKE '%:%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_NewsComments",
                table: "NewsComments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NewsCategories",
                table: "NewsCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_News",
                table: "News");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "NewsComments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "NewsCategories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "News");

            migrationBuilder.RenameColumn(
                name: "Key",
                table: "NewsComments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "Key",
                table: "NewsCategories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "Key",
                table: "News",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NewsComments",
                table: "NewsComments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NewsCategories",
                table: "NewsCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_News",
                table: "News",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_News_Highlighted",
                table: "News",
                column: "Highlighted",
                unique: true,
                filter: "\"Highlighted\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_News_NewsCategories_CategoryId",
                table: "News",
                column: "CategoryId",
                principalTable: "NewsCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsComments_News_NewsId",
                table: "NewsComments",
                column: "NewsId",
                principalTable: "News",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

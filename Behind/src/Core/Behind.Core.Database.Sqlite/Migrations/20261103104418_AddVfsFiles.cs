using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddVfsFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VfsFiles",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VfsFiles", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateIndex(
                name: "IX_VfsFiles_ParentId",
                table: "VfsFiles",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_VfsFiles_Type",
                table: "VfsFiles",
                column: "Type");

            migrationBuilder.Sql(
                """
                INSERT INTO "VfsFiles" ("Key", "Version", "Type", "FileName", "ContentType", "Length", "ParentId")
                SELECT "Id", 1, 'Sites', "FileName", "ContentType", length("Content"), NULL FROM "SiteImages";
                """);

            migrationBuilder.Sql(AttachSiteImages("Sites", "Sites", "SiteId", "\"Content\" LIKE '%/sites/images/' || \"VfsFiles\".\"Key\" || '%'"));
            migrationBuilder.Sql(AttachSiteImages("News", "News", "Key", "\"Summary\" LIKE '%/sites/images/' || \"VfsFiles\".\"Key\" || '%' OR \"Content\" LIKE '%/sites/images/' || \"VfsFiles\".\"Key\" || '%'"));
            migrationBuilder.Sql(AttachSiteImages("Articles", "Articles", "Key", "\"Summary\" LIKE '%/sites/images/' || \"VfsFiles\".\"Key\" || '%' OR \"Content\" LIKE '%/sites/images/' || \"VfsFiles\".\"Key\" || '%'"));
            migrationBuilder.Sql(AttachSiteImages("NewsCategories", "NewsCategories", "Key", "\"HeaderImageId\" = \"VfsFiles\".\"Key\""));
            migrationBuilder.Sql(AttachSiteImages("ArticleCategories", "ArticleCategories", "Key", "\"HeaderImageId\" = \"VfsFiles\".\"Key\""));

            migrationBuilder.Sql(
                """
                INSERT INTO "VfsFiles" ("Key", "Version", "Type", "FileName", "ContentType", "Length", "ParentId")
                SELECT
                    "Key", "Version", 'Galleries', "FileName", "ContentType", length("Content"),
                    COALESCE(
                        (SELECT "GalleryId" FROM "GalleryItems"
                         WHERE "ImageId" = "GalleryImages"."Key" OR "ThumbnailId" = "GalleryImages"."Key" LIMIT 1),
                        (SELECT "Key" FROM "Galleries" WHERE "ThumbnailImageId" = "GalleryImages"."Key" LIMIT 1))
                FROM "GalleryImages";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ArticleCategories_SiteImages_HeaderImageId",
                table: "ArticleCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsCategories_SiteImages_HeaderImageId",
                table: "NewsCategories");

            migrationBuilder.DropIndex(
                name: "IX_NewsCategories_HeaderImageId",
                table: "NewsCategories");

            migrationBuilder.DropIndex(
                name: "IX_ArticleCategories_HeaderImageId",
                table: "ArticleCategories");
        }

        private static string AttachSiteImages(string type, string table, string parentColumn, string condition) =>
            $"""
             UPDATE "VfsFiles"
             SET "Type" = '{type}', "ParentId" = (SELECT "{parentColumn}" FROM "{table}" WHERE {condition} LIMIT 1)
             WHERE "Type" = 'Sites' AND "ParentId" IS NULL
               AND EXISTS (SELECT 1 FROM "{table}" WHERE {condition});
             """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VfsFiles");

            migrationBuilder.CreateIndex(
                name: "IX_NewsCategories_HeaderImageId",
                table: "NewsCategories",
                column: "HeaderImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCategories_HeaderImageId",
                table: "ArticleCategories",
                column: "HeaderImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleCategories_SiteImages_HeaderImageId",
                table: "ArticleCategories",
                column: "HeaderImageId",
                principalTable: "SiteImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsCategories_SiteImages_HeaderImageId",
                table: "NewsCategories",
                column: "HeaderImageId",
                principalTable: "SiteImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

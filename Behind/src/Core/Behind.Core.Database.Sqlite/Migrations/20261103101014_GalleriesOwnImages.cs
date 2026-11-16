using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class GalleriesOwnImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Galleries_SiteImages_ThumbnailImageId",
                table: "Galleries");

            migrationBuilder.DropForeignKey(
                name: "FK_GalleryItems_SiteImages_ImageId",
                table: "GalleryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_GalleryItems_SiteImages_ThumbnailId",
                table: "GalleryItems");

            migrationBuilder.DropIndex(
                name: "IX_Galleries_ThumbnailImageId",
                table: "Galleries");

            migrationBuilder.CreateTable(
                name: "GalleryImages",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryImages", x => new { x.Key, x.Version });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GalleryImages");

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_ThumbnailImageId",
                table: "Galleries",
                column: "ThumbnailImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Galleries_SiteImages_ThumbnailImageId",
                table: "Galleries",
                column: "ThumbnailImageId",
                principalTable: "SiteImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GalleryItems_SiteImages_ImageId",
                table: "GalleryItems",
                column: "ImageId",
                principalTable: "SiteImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GalleryItems_SiteImages_ThumbnailId",
                table: "GalleryItems",
                column: "ThumbnailId",
                principalTable: "SiteImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

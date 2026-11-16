using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Galleries",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThumbnailGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    ThumbnailImageId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Galleries", x => new { x.Key, x.Version });
                    table.ForeignKey(
                        name: "FK_Galleries_SiteImages_ThumbnailImageId",
                        column: x => x.ThumbnailImageId,
                        principalTable: "SiteImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Galleries_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GalleryItems",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    GalleryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ThumbnailId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryItems", x => new { x.Key, x.Version });
                    table.ForeignKey(
                        name: "FK_GalleryItems_SiteImages_ImageId",
                        column: x => x.ImageId,
                        principalTable: "SiteImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GalleryItems_SiteImages_ThumbnailId",
                        column: x => x.ThumbnailId,
                        principalTable: "SiteImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_AuthorId",
                table: "Galleries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_ThumbnailImageId",
                table: "Galleries",
                column: "ThumbnailImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_Title",
                table: "Galleries",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_GalleryId_Version",
                table: "GalleryItems",
                columns: new[] { "GalleryId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_ImageId",
                table: "GalleryItems",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_ThumbnailId",
                table: "GalleryItems",
                column: "ThumbnailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Galleries");

            migrationBuilder.DropTable(
                name: "GalleryItems");
        }
    }
}

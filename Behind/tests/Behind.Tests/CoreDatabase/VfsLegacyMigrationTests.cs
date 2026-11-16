using Behind.Common.Storage;
using Behind.Core.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class VfsLegacyMigrationTests : IDisposable
{
    private const string BeforeVfs = "20261103101014_GalleriesOwnImages";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"behind-legacy-{Guid.NewGuid():N}");

    public VfsLegacyMigrationTests()
    {
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private CoreContextSqlite CreateContext() =>
        new(new DbContextOptionsBuilder<CoreContextSqlite>().UseSqlite(_connection).Options);

    private IContentStore Store => new DirectoryContentStore(_root);

    private static byte[]? Bytes(Option<byte[]> content) => content.Match<byte[]?>(() => null, bytes => bytes);

    private async Task SeedLegacyAsync(Guid siteImage, Guid galleryImage, Guid category, Guid gallery, Guid site)
    {
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(BeforeVfs);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "SiteImages" ("Id", "FileName", "ContentType", "Content", "CreatedAt")
             VALUES ({siteImage}, {"logo.png"}, {"image/png"}, {new byte[] { 1, 2, 3 }}, {0})
             """);
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "GalleryImages" ("Key", "Version", "FileName", "ContentType", "Content")
             VALUES ({galleryImage}, {1L}, {"sunset.webp"}, {"image/webp"}, {new byte[] { 9, 8 }})
             """);
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "NewsCategories" ("Key", "Version", "HeaderImageId", "Name")
             VALUES ({category}, {1L}, {siteImage}, {"Releases"})
             """);
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "Sites" ("SiteId", "Version", "Title", "Slug", "Content", "CreatedAt", "PublishedAtUtc")
             VALUES ({site}, {1L}, {"About"}, {"about"}, {$"<img src=\"/sites/images/{siteImage}\">"}, {0}, {null as long?})
             """);
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "GalleryItems" ("Key", "Version", "Description", "GalleryId", "ImageId", "Position", "ThumbnailId")
             VALUES ({Guid.NewGuid()}, {1L}, {"Sunset"}, {gallery}, {galleryImage}, {0}, {Guid.NewGuid()})
             """);
    }

    private async Task<List<(string Type, Guid? ParentId)>> ParentsAsync()
    {
        await using var context = CreateContext();

        return (await context.VfsFiles.ToListAsync()).Select(file => (file.Type, file.ParentId)).ToList();
    }

    private async Task<List<(string Type, string FileName, string ContentType, long Length)>> FilesAsync()
    {
        await using var context = CreateContext();

        return (await context.VfsFiles.OrderBy(file => file.Type).ToListAsync())
            .Select(file => (file.Type, file.FileName, file.ContentType, file.Length))
            .ToList();
    }

    [Fact]
    public async Task MigrateWithVfs_MovesTheLegacyImagesIntoTheVfs()
    {
        Guid siteImage = Guid.NewGuid(), galleryImage = Guid.NewGuid(), category = Guid.NewGuid(), gallery = Guid.NewGuid(), site = Guid.NewGuid();
        await SeedLegacyAsync(siteImage, galleryImage, category, gallery, site);

        await using (var context = CreateContext())
        {
            await context.MigrateWithVfsAsync(Store);
        }

        Assert.Equal(
            [
                (VfsTypes.Galleries, "sunset.webp", "image/webp", 2L),
                (VfsTypes.Sites, "logo.png", "image/png", 3L)
            ],
            await FilesAsync());
        Assert.Equal(
            new byte[] { 1, 2, 3 },
            Bytes(await Store.Read(VfsTestPaths.Of(VfsTypes.Sites, siteImage, site))));
        Assert.Equal(
            new byte[] { 9, 8 },
            Bytes(await Store.Read(VfsTestPaths.Of(VfsTypes.Galleries, galleryImage, gallery))));
        var parents = await ParentsAsync();
        Assert.Contains((VfsTypes.Sites, (Guid?)site), parents);
        Assert.Contains((VfsTypes.Galleries, (Guid?)gallery), parents);
    }

    [Fact]
    public async Task MigrateWithVfs_KeepsTheHeaderImagesOfTheCategories()
    {
        Guid siteImage = Guid.NewGuid(), galleryImage = Guid.NewGuid(), category = Guid.NewGuid(), gallery = Guid.NewGuid(), site = Guid.NewGuid();
        await SeedLegacyAsync(siteImage, galleryImage, category, gallery, site);

        await using (var context = CreateContext())
        {
            await context.MigrateWithVfsAsync(Store);
        }

        await using var read = CreateContext();
        var headers = await read.Database
            .SqlQuery<Guid?>($"""SELECT "HeaderImageId" AS "Value" FROM "NewsCategories" """)
            .ToListAsync();
        Assert.Equal([siteImage], headers.OfType<Guid>());
    }

    [Fact]
    public async Task MigrateWithVfs_DropsTheLegacyTables()
    {
        Guid siteImage = Guid.NewGuid(), galleryImage = Guid.NewGuid(), category = Guid.NewGuid(), gallery = Guid.NewGuid(), site = Guid.NewGuid();
        await SeedLegacyAsync(siteImage, galleryImage, category, gallery, site);

        await using (var context = CreateContext())
        {
            await context.MigrateWithVfsAsync(Store);
        }

        await using var read = CreateContext();
        var tables = await read.Database
            .SqlQuery<string>($"""SELECT "name" AS "Value" FROM "sqlite_master" WHERE "type" = 'table' """)
            .ToListAsync();
        Assert.DoesNotContain("SiteImages", tables);
        Assert.DoesNotContain("GalleryImages", tables);
        Assert.Contains("VfsFiles", tables);
    }

    [Fact]
    public async Task MigrateWithVfs_OnAnEmptyDatabase_CreatesTheCurrentSchema()
    {
        await using (var context = CreateContext())
        {
            await context.MigrateWithVfsAsync(Store);
        }

        await using var read = CreateContext();
        Assert.Empty(await read.Database.GetPendingMigrationsAsync());
        Assert.Empty(await FilesAsync());
    }

    [Fact]
    public async Task MigrateWithVfs_RunTwice_IsANoOp()
    {
        Guid siteImage = Guid.NewGuid(), galleryImage = Guid.NewGuid(), category = Guid.NewGuid(), gallery = Guid.NewGuid(), site = Guid.NewGuid();
        await SeedLegacyAsync(siteImage, galleryImage, category, gallery, site);

        await using (var context = CreateContext())
        {
            await context.MigrateWithVfsAsync(Store);
            await context.MigrateWithVfsAsync(Store);
        }

        Assert.Equal(2, (await FilesAsync()).Count);
    }

    [Fact]
    public async Task MigrateWithVfs_UsesTheItemThatUsesTheImageAsItsParent()
    {
        Guid siteImage = Guid.NewGuid(), galleryImage = Guid.NewGuid(), category = Guid.NewGuid(), gallery = Guid.NewGuid(), site = Guid.NewGuid();
        Guid inNews = Guid.NewGuid(), inArticle = Guid.NewGuid(), articleHeader = Guid.NewGuid(), unused = Guid.NewGuid();
        Guid news = Guid.NewGuid(), article = Guid.NewGuid(), articleCategory = Guid.NewGuid();
        await SeedLegacyAsync(siteImage, galleryImage, category, gallery, site);

        await using (var seed = CreateContext())
        {
            await seed.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = 0");

            foreach (var id in new[] { inNews, inArticle, articleHeader, unused })
            {
                await seed.Database.ExecuteSqlAsync(
                    $"""
                     INSERT INTO "SiteImages" ("Id", "FileName", "ContentType", "Content", "CreatedAt")
                     VALUES ({id}, {"x.png"}, {"image/png"}, {new byte[] { 1 }}, {0})
                     """);
            }

            await seed.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "News" ("Key", "Version", "AuthorId", "Highlighted", "Title", "Summary", "Content")
                 VALUES ({news}, {1L}, {Guid.NewGuid()}, {false}, {"n"}, {$"<img src=\"/sites/images/{inNews}\">"}, {"c"})
                 """);
            await seed.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "Articles" ("Key", "Version", "AuthorId", "Title", "Summary", "Content")
                 VALUES ({article}, {1L}, {Guid.NewGuid()}, {"a"}, {"s"}, {$"<img src=\"/sites/images/{inArticle}\">"})
                 """);
            await seed.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO "ArticleCategories" ("Key", "Version", "HeaderImageId", "Name")
                 VALUES ({articleCategory}, {1L}, {articleHeader}, {"Guides"})
                 """);
        }

        await using (var context = CreateContext())
        {
            await context.MigrateWithVfsAsync(Store);
        }

        await using var read = CreateContext();
        var files = (await read.VfsFiles
                .Select(file => new { Key = EF.Property<Guid>(file, VersionedColumns.Key), file.Type, file.ParentId })
                .ToListAsync())
            .ToDictionary(file => file.Key, file => (file.Type, file.ParentId));
        Assert.Equal((VfsTypes.News, (Guid?)news), files[inNews]);
        Assert.Equal((VfsTypes.Articles, (Guid?)article), files[inArticle]);
        Assert.Equal((VfsTypes.ArticleCategories, (Guid?)articleCategory), files[articleHeader]);
        Assert.Equal((VfsTypes.Sites, (Guid?)site), files[siteImage]);
        Assert.Equal((VfsTypes.Sites, (Guid?)null), files[unused]);
    }
}

using System.Reflection;
using Behind.Common.Storage;
using Behind.Core.Database.Sqlite.Migrations;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite;

public static class VfsLegacyMigrationExtensions
{
    private const string SiteImagesQuery =
        """
         SELECT f."Key", f."Type", f."ParentId", i."Content" FROM "VfsFiles" f
         JOIN "SiteImages" i ON i."Id" = f."Key"
         """;

    private const string GalleryImagesQuery =
        """
         SELECT f."Key", f."Type", f."ParentId", i."Content" FROM "VfsFiles" f
         JOIN "GalleryImages" i ON i."Key" = f."Key" AND i."Version" = f."Version"
         """;

    private static readonly string FilesAdded = IdOf<AddVfsFiles>();

    private static readonly string LegacyDropped = IdOf<DropLegacyImages>();

    private sealed record LegacyImage(Guid Key, string Type, Guid? ParentId, byte[] Content);

    extension(CoreContextSqlite context)
    {
        public async Task MigrateWithVfsAsync(IContentStore store, CancellationToken cancellationToken = default)
        {
            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);

            if (pending.Contains(LegacyDropped))
            {
                await context.GetService<IMigrator>().MigrateAsync(FilesAdded, cancellationToken);
                await context.MoveLegacyImagesAsync(store, cancellationToken);
            }

            await context.Database.MigrateAsync(cancellationToken);
        }

        private async Task MoveLegacyImagesAsync(IContentStore store, CancellationToken cancellationToken)
        {
            foreach (var query in new[] { SiteImagesQuery, GalleryImagesQuery })
            {
                await foreach (var image in context.Database
                                   .SqlQueryRaw<LegacyImage>(query)
                                   .AsAsyncEnumerable()
                                   .WithCancellation(cancellationToken))
                {
                    await store.Write(
                        new VfsFile(image.Type, string.Empty, string.Empty, image.Content.LongLength, image.ParentId)
                            .StorageKeyOf(image.Key),
                        image.Content,
                        cancellationToken);
                }
            }
        }
    }

    private static string IdOf<TMigration>() where TMigration : Migration =>
        typeof(TMigration).GetCustomAttribute<MigrationAttribute>()!.Id;
}

using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Storage;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class VfsCommands(AbstractCoreContext context, IContentStore store) : IVfsCommands
{
    private sealed record Move(VfsFile From, VfsFile To);

    public async Task<Guid> Add(IVfsCommands.Upload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);

        var key = Guid.CreateVersion7();
        var file = new VfsFile(upload.Type, upload.FileName, upload.ContentType, upload.Content.LongLength);
        var storageKey = file.StorageKeyOf(key);

        await store.Write(storageKey, upload.Content, cancellationToken);

        try
        {
            await context.VfsFiles.AddAsync(file, cancellationToken);
            context.Entry(file).StampVersionedKey(key, VersionedColumns.FirstVersion);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await store.Delete(storageKey, CancellationToken.None);

            throw;
        }

        return key;
    }

    public async Task Attach(
        string type,
        Guid parentId,
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            return;
        }

        var moves = (await context.VfsFiles.InSameKindAs(type).OfKeys(keys).ToFilesAsync(cancellationToken))
            .Latest()
            .Where(file => file.ParentId is null)
            .Select(file => new Move(
                file,
                file with
                {
                    Id = new VersionedKey<Guid>(file.Id.Key, file.Id.Version + 1),
                    Type = type,
                    ParentId = parentId
                }))
            .ToList();

        foreach (var move in moves)
        {
            await store.Move(move.From.StorageKey, move.To.StorageKey, cancellationToken);
        }

        try
        {
            foreach (var move in moves)
            {
                await context.VfsFiles.AddAsync(move.To, cancellationToken);
                context.Entry(move.To).StampVersionedKey(move.To.Id.Key, move.To.Id.Version);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var move in moves)
            {
                await store.Move(move.To.StorageKey, move.From.StorageKey, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task Delete(IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var files = await context.VfsFiles.OfKeys(keys).ToListAsync(cancellationToken);
        var storageKeys = files
            .GroupBy(file => context.Entry(file).VersionedId.Key)
            .Select(versions => versions
                .MaxBy(file => context.Entry(file).VersionedId.Version)!
                .StorageKeyOf(versions.Key))
            .ToList();

        context.VfsFiles.RemoveRange(files);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var storageKey in storageKeys)
        {
            await store.Delete(storageKey, cancellationToken);
        }
    }
}

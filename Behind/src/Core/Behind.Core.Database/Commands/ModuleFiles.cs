using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class ModuleFiles(AbstractCoreContext context, IVfsCommands commands, IVfsQueries queries) : IModuleFiles
{
    public Task<Guid> Add(IModuleFiles.Upload upload, CancellationToken cancellationToken = default) =>
        commands.Add(
            new IVfsCommands.Upload(upload.Type, upload.FileName, upload.ContentType, upload.Content),
            cancellationToken);

    public Task Attach(
        string type,
        Guid parentId,
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken = default) =>
        commands.Attach(type, parentId, keys, cancellationToken);

    public Task<bool> Exists(string type, Guid key, CancellationToken cancellationToken = default) =>
        context.VfsFiles.InTypes([type]).OfKey(key).AnyAsync(cancellationToken);

    public async Task<Option<IModuleFiles.FileContent>> Open(
        string type,
        Guid key,
        CancellationToken cancellationToken = default) =>
        (await queries.Open([type], key, cancellationToken))
        .Map(content => new IModuleFiles.FileContent(content.File.ContentType, content.Bytes));
}

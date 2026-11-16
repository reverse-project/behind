using System.Collections.Concurrent;
using Behind.Common.Database.Modules;

namespace Behind.Tests.Helpers;

public sealed class FakeModuleFiles : IModuleFiles
{
    private readonly ConcurrentDictionary<(string Type, Guid Key), IModuleFiles.FileContent> _files = new();
    private readonly ConcurrentQueue<(string Type, Guid ParentId, IReadOnlyCollection<Guid> Keys)> _attached = new();

    public IReadOnlyList<(string Type, Guid ParentId, IReadOnlyCollection<Guid> Keys)> Attached => _attached.ToList();

    public Task<Guid> Add(IModuleFiles.Upload upload, CancellationToken cancellationToken = default)
    {
        var key = Guid.CreateVersion7();
        _files[(upload.Type, key)] = new IModuleFiles.FileContent(upload.ContentType, upload.Content);

        return Task.FromResult(key);
    }

    public Task Attach(
        string type,
        Guid parentId,
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken = default)
    {
        _attached.Enqueue((type, parentId, keys));

        return Task.CompletedTask;
    }

    public Task<bool> Exists(string type, Guid key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.ContainsKey((type, key)));

    public Task<Option<IModuleFiles.FileContent>> Open(
        string type,
        Guid key,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            _files.TryGetValue((type, key), out var content)
                ? Some(content)
                : None);
}

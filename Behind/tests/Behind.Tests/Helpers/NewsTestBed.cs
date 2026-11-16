using Behind.Common.Storage;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.Helpers;

public sealed class NewsTestBed : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

    public NewsTestBed()
    {
        ContentStore = new DirectoryContentStore(ContentRoot);
        using var seed = _factory.CreateContext();
        AuthorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "author@test.com").GetAwaiter().GetResult();
        OtherAuthorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "other@test.com").GetAwaiter().GetResult();
        DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
        Actor = AuditActor.ForUser(AuthorId);
    }

    public IContentStore ContentStore { get; }

    public string ContentRoot { get; } = Path.Combine(Path.GetTempPath(), $"behind-vfs-{Guid.NewGuid():N}");

    public Guid AuthorId { get; }

    public Guid OtherAuthorId { get; }

    public AuditActor Actor { get; }

    public CoreContextSqlite Read() => _factory.CreateContext();

    public VersioningContextSqlite ReadVersions() => _factory.CreateVersioningContext();

    public CoreContextSqlite Write(DateTimeOffset? at = null) =>
        _factory.CreateAuditedContext(Actor, new FixedTimeProvider(at ?? Now));

    public VersioningContextSqlite WriteVersions(DateTimeOffset? at = null) =>
        _factory.CreateAuditedVersioningContext(Actor, new FixedTimeProvider(at ?? Now));

    public async Task<TResult> WithNewsCommandsAsync<TResult>(
        Func<NewsCommands, Task<TResult>> action,
        DateTimeOffset? at = null)
    {
        await using var ctx = Write(at);
        await using var versions = WriteVersions(at);

        return await action(new NewsCommands(ctx, versions, new VfsCommands(ctx, ContentStore)));
    }

    public async Task<VersionedKey<Guid>> SaveNewsAsync(
        Guid? key,
        string title = "Headline",
        Option<Guid> categoryId = default,
        DateTimeOffset? at = null,
        Guid? authorId = null,
        string summary = "<p>short</p>",
        string content = "<p>long</p>",
        bool highlighted = false,
        Option<DateTimeOffset> startAt = default,
        Option<DateTimeOffset> endAt = default,
        bool publish = true)
    {
        var draft = new INewsCommands.NewsDraft(title, summary, content, categoryId, highlighted, startAt, endAt);

        var result = await WithNewsCommandsAsync(
            commands => key is { } existing
                ? commands.SaveVersion(existing, draft, publish)
                : commands.Create(authorId ?? AuthorId, draft, publish),
            at);

        return Assert.IsType<NewsSaveResult.Saved>(result).Id;
    }

    public async Task<Guid> CreateNewsAsync(
        string title = "Headline",
        Option<Guid> categoryId = default,
        DateTimeOffset? at = null,
        Guid? authorId = null,
        string summary = "<p>short</p>",
        string content = "<p>long</p>",
        bool highlighted = false,
        Option<DateTimeOffset> startAt = default,
        Option<DateTimeOffset> endAt = default,
        bool publish = true) =>
        (await SaveNewsAsync(null, title, categoryId, at, authorId, summary, content, highlighted, startAt, endAt, publish)).Key;

    public async Task<Guid> CreateCategoryAsync(string name = "Releases", Option<Guid> headerImageId = default)
    {
        await using var ctx = Write();
        var result = await new NewsCategoryCommands(ctx, new VfsCommands(ctx, ContentStore)).Create(
            new INewsCategoryCommands.NewsCategoryDraft(name, headerImageId));

        return Assert.IsType<NewsCategorySaveResult.Saved>(result).Id.Key;
    }

    public async Task<Guid> AddCommentAsync(
        Guid newsId,
        string content = "Nice",
        Guid? authorId = null,
        DateTimeOffset? at = null)
    {
        await using var ctx = Write(at);
        var added = await new NewsCommentCommands(ctx, new FixedTimeProvider(at ?? Now))
            .Add(newsId, authorId ?? AuthorId, content);

        return added.Match(() => throw new InvalidOperationException(), id => id);
    }

    public async Task<Guid> AddVfsFileAsync(
        string directory,
        string fileName = "photo.png",
        string contentType = "image/png",
        byte[]? content = null)
    {
        await using var ctx = Write();

        return await new VfsCommands(ctx, ContentStore).Add(
            new IVfsCommands.Upload(directory, fileName, contentType, content ?? [1, 2, 3]));
    }

    public Task<Guid> AddImageAsync(string fileName = "header.png") =>
        AddVfsFileAsync(VfsTypes.Sites, fileName);

    public async Task<AuditTimes> TimesAsync<TEntity>(Guid key, long shownVersion) where TEntity : class
    {
        await using var ctx = Read();
        var times = await ctx.AuditTimesAsync<TEntity>(new Dictionary<Guid, long> { [key] = shownVersion });

        return times[key];
    }

    public async Task<IReadOnlyList<string>> ActionsAsync(string entityName, Guid key, long version = 1)
    {
        await using var ctx = Read();
        var entityKey = $"{key.ToString().ToLowerInvariant()}:{version}";
        var entries = await ctx.AuditLogEntries
            .Where(entry => entry.EntityName == entityName && entry.EntityKey == entityKey)
            .OrderBy(entry => entry.Timestamp)
            .ToListAsync();

        return entries.Select(entry => entry.Action.GetType().Name).ToList();
    }

    public void Dispose()
    {
        _factory.Dispose();

        if (Directory.Exists(ContentRoot))
        {
            Directory.Delete(ContentRoot, true);
        }
    }
}

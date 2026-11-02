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
        using var seed = _factory.CreateContext();
        AuthorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "author@test.com").GetAwaiter().GetResult();
        OtherAuthorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "other@test.com").GetAwaiter().GetResult();
        DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
        Actor = AuditActor.ForUser(AuthorId);
    }

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

        return await action(new NewsCommands(ctx, versions));
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
        var result = await new NewsCategoryCommands(ctx).Create(
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

    public async Task<Guid> AddImageAsync()
    {
        await using var ctx = Write();

        return await new SiteImageCommands(ctx).Add(new SiteImage("header.png", "image/png", [1, 2, 3], Now));
    }

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

    public void Dispose() => _factory.Dispose();
}

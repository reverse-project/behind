using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Quotes.Database;
using Behind.Quotes.Database.Commands;
using Behind.Quotes.Database.Queries;
using Behind.Quotes.WebApi;
using Behind.Sport.WebApi;
using Behind.Web.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Behind.Tests.Helpers;

public sealed class QuotesTestBed : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly ModuleCatalog Catalog =
        ModuleCatalog.Create([QuotesModule.Descriptor, SportModule.Descriptor, Behind.Forum.WebApi.ForumModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public QuotesTestBed()
    {
        _connection.Open();
        using var context = Read();
        context.Database.EnsureCreated();
        context.EnsureBuiltInUsersAsync().GetAwaiter().GetResult();
    }

    public WebContextSqlite Read() =>
        new(new DbContextOptionsBuilder<WebContextSqlite>().UseSqlite(_connection).Options, Catalog);

    public WebContextSqlite Write() =>
        new(
            new DbContextOptionsBuilder<WebContextSqlite>()
                .UseSqlite(_connection)
                .AddInterceptors(new AuditSaveChangesInterceptor(
                    () => new FixedAuditActorProvider(AuditActor.Autonomous),
                    new FixedTimeProvider(Now)))
                .Options,
            Catalog);

    public WebContextSqlite WriteFailing() =>
        new(
            new DbContextOptionsBuilder<WebContextSqlite>()
                .UseSqlite(_connection)
                .AddInterceptors(new RejectingSaveInterceptor())
                .Options,
            Catalog);

    public async Task<TResult> WithCommandsAsync<TResult>(Func<QuoteCommands, Task<TResult>> action)
    {
        await using var context = Write();

        return await action(new QuoteCommands(context));
    }

    public async Task<TResult> WithQueriesAsync<TResult>(Func<QuoteQueries, Task<TResult>> action)
    {
        await using var context = Read();

        return await action(new QuoteQueries(context));
    }

    public async Task<Guid> CreateAsync(string text = "Keep it simple", string author = "Ada")
    {
        var result = await WithCommandsAsync(commands => commands.Create(new IQuoteCommands.QuoteDraft(text, author)));

        return Assert.IsType<QuoteSaveResult.Saved>(result).Id.Key;
    }

    public async Task<IReadOnlyList<IQuoteQueries.QuoteListing>> ListAsync(
        DeletionState state = DeletionState.Active,
        Option<string> search = default,
        Option<string> letter = default)
    {
        var page = await WithQueriesAsync(queries => queries.List(new Flipper(1, 100), search, letter, state));

        return page.Results.Match(() => (IReadOnlyList<IQuoteQueries.QuoteListing>)[], items => items.ToList());
    }

    public async Task<IReadOnlyList<string>> ActionsAsync(Guid key, long version = 1)
    {
        await using var context = Read();
        var entityKey = $"{key.ToString().ToLowerInvariant()}:{version}";
        var entries = await context.AuditLogEntries
            .Where(entry => entry.EntityName == "Quote" && entry.EntityKey == entityKey)
            .OrderBy(entry => entry.Timestamp)
            .ToListAsync();

        return entries.Select(entry => entry.Action.GetType().Name).ToList();
    }

    public void Dispose() => _connection.Dispose();
}

public sealed class RejectingSaveInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("The save is rejected.");
}

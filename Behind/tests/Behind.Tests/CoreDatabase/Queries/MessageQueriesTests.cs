using Behind.Core.Database;
using Behind.Core.Database.Queries;
using static Behind.Tests.Helpers.MessageTestBedExtensions;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class MessageQueriesTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(2);

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<IMessageQueries.MessageListing>> ListAsync(
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active,
        Flipper? flipper = null)
    {
        await using var ctx = _bed.Read();

        return await new MessageQueries(ctx).List(flipper ?? new Flipper(1, 10), search, letter, state);
    }

    private static IReadOnlyList<string> Names(PagedResult<IMessageQueries.MessageListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Name).ToList());

    private Task<Option<IMessageQueries.MessageDetails>> FindLiveAsync(Guid key, DateTimeOffset now) =>
        _bed.WithMessageQueriesAsync(queries => queries.FindLive(key, now));

    [Fact]
    public async Task List_ReturnsTheLatestVersionOfEveryMessageOrderedByName()
    {
        var outage = await _bed.CreateMessageAsync(MessageDraft("outage", "Old"));
        await _bed.WithMessageCommandsAsync(commands =>
            commands.Update(outage, MessageDraft("outage", "New", level: MessageLevel.Danger, startsAt: Start, endsAt: End)));
        await _bed.CreateMessageAsync(MessageDraft("banner"));

        var page = await ListAsync();

        Assert.Equal(2, page.Total);
        Assert.Equal(["banner", "outage"], Names(page));
        var listing = page.Results.GetOrElse([]).Last();
        Assert.Equal(new VersionedKey<Guid>(outage, 2), listing.Id);
        Assert.Equal(("New", MessageLevel.Danger, Start, End), (listing.Title, listing.Level, listing.StartsAt, listing.EndsAt));
    }

    [Fact]
    public async Task List_SearchMatchesTheNameAndTheTitle()
    {
        await _bed.CreateMessageAsync(MessageDraft("outage", "Server Maintenance"));
        await _bed.CreateMessageAsync(MessageDraft("banner", "Welcome"));

        Assert.Equal(["outage"], Names(await ListAsync(Some("OUT"))));
        Assert.Equal(["outage"], Names(await ListAsync(Some("maintenance"))));
        Assert.Equal(["banner"], Names(await ListAsync(Some("welc"))));
        Assert.Equal(2, (await ListAsync(Some("  "))).Total);
    }

    [Fact]
    public async Task List_LetterFiltersByTheFirstLetterOfTheName()
    {
        await _bed.CreateMessageAsync(MessageDraft("outage"));
        await _bed.CreateMessageAsync(MessageDraft("banner"));

        Assert.Equal(["banner"], Names(await ListAsync(letter: Some("B"))));
    }

    [Fact]
    public async Task List_PagesTheResults()
    {
        await _bed.CreateMessageAsync(MessageDraft("a"));
        await _bed.CreateMessageAsync(MessageDraft("b"));
        await _bed.CreateMessageAsync(MessageDraft("c"));

        var page = await ListAsync(flipper: new Flipper(2, 2));

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.PageCount);
        Assert.Equal(["c"], Names(page));
    }

    [Fact]
    public async Task List_DeletedState_ReturnsOnlyDeletedMessages()
    {
        await _bed.CreateMessageAsync(MessageDraft("outage"));
        var gone = await _bed.CreateMessageAsync(MessageDraft("banner"));
        await _bed.WithMessageCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["banner"], Names(await ListAsync(state: DeletionState.Deleted)));
        Assert.Equal(["outage"], Names(await ListAsync()));
    }

    [Fact]
    public async Task GetLetters_ReturnsTheFirstLettersOfTheMatchingNames()
    {
        await _bed.CreateMessageAsync(MessageDraft("outage"));
        await _bed.CreateMessageAsync(MessageDraft("banner"));

        Assert.Equal(["B", "O"], await _bed.WithMessageQueriesAsync(queries => queries.GetLetters(None, DeletionState.Active)));
    }

    [Fact]
    public async Task Find_ReturnsTheLatestVersionWithItsContent()
    {
        var key = await _bed.CreateMessageAsync(MessageDraft(content: "<p>Old</p>"));
        await _bed.WithMessageCommandsAsync(commands => commands.Update(key, MessageDraft(content: "<p>New</p>")));

        var found = (await _bed.WithMessageQueriesAsync(queries => queries.Find(key, DeletionState.Active))).GetOrElse(() => (IMessageQueries.MessageDetails)null!);

        Assert.Equal(new VersionedKey<Guid>(key, 2), found.Id);
        Assert.Equal("<p>New</p>", found.Content);
    }

    [Fact]
    public async Task Find_DeletedMessage_IsFoundOnlyInTheDeletedState()
    {
        var key = await _bed.CreateMessageAsync();
        await _bed.WithMessageCommandsAsync(commands => commands.Delete(key));

        Assert.Equal(None, await _bed.WithMessageQueriesAsync(queries => queries.Find(key, DeletionState.Active)));
        Assert.NotEqual(None, await _bed.WithMessageQueriesAsync(queries => queries.Find(key, DeletionState.Deleted)));
        Assert.Equal(None, await _bed.WithMessageQueriesAsync(queries => queries.Find(Guid.NewGuid(), DeletionState.Deleted)));
    }

    [Fact]
    public async Task FindLive_IncludesTheStartAndExcludesTheEnd()
    {
        var key = await _bed.CreateMessageAsync(MessageDraft(startsAt: Start, endsAt: End));

        Assert.Equal(None, await FindLiveAsync(key, Start.AddTicks(-1)));
        Assert.NotEqual(None, await FindLiveAsync(key, Start));
        Assert.NotEqual(None, await FindLiveAsync(key, End.AddTicks(-1)));
        Assert.Equal(None, await FindLiveAsync(key, End));
    }

    [Fact]
    public async Task FindLive_ComparesInstantsRegardlessOfTheOffset()
    {
        var key = await _bed.CreateMessageAsync(MessageDraft(startsAt: Start.ToOffset(TimeSpan.FromHours(2)), endsAt: End));

        Assert.NotEqual(None, await FindLiveAsync(key, Start.ToOffset(TimeSpan.FromHours(-5))));
        Assert.Equal(None, await FindLiveAsync(key, End.ToOffset(TimeSpan.FromHours(9))));
    }

    [Fact]
    public async Task FindLive_OpenBoundsAreAlwaysLive()
    {
        var always = await _bed.CreateMessageAsync();
        var fromStart = await _bed.CreateMessageAsync(MessageDraft("from", startsAt: Start));
        var untilEnd = await _bed.CreateMessageAsync(MessageDraft("until", endsAt: End));

        Assert.NotEqual(None, await FindLiveAsync(always, DateTimeOffset.MinValue.AddYears(1)));
        Assert.Equal(None, await FindLiveAsync(fromStart, Start.AddTicks(-1)));
        Assert.NotEqual(None, await FindLiveAsync(fromStart, Start.AddYears(10)));
        Assert.NotEqual(None, await FindLiveAsync(untilEnd, Start.AddYears(-10)));
        Assert.Equal(None, await FindLiveAsync(untilEnd, End));
    }

    [Fact]
    public async Task FindLive_DeletedMessage_IsNone()
    {
        var key = await _bed.CreateMessageAsync(MessageDraft(startsAt: Start, endsAt: End));
        await _bed.WithMessageCommandsAsync(commands => commands.Delete(key));

        Assert.Equal(None, await FindLiveAsync(key, Start));
    }

    [Fact]
    public async Task FindLive_UsesTheScheduleOfTheLatestVersion()
    {
        var key = await _bed.CreateMessageAsync(MessageDraft(startsAt: Start, endsAt: End));
        await _bed.WithMessageCommandsAsync(commands => commands.Update(key, MessageDraft(startsAt: End, endsAt: End.AddHours(1))));

        Assert.Equal(None, await FindLiveAsync(key, Start));
        Assert.Equal(new VersionedKey<Guid>(key, 2), (await FindLiveAsync(key, End)).GetOrElse(() => (IMessageQueries.MessageDetails)null!).Id);
    }
}

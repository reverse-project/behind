using Behind.Quotes.Database;

namespace Behind.Tests.Quotes;

public sealed class QuoteQueriesTests : IDisposable
{
    private readonly QuotesTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task SeedAsync()
    {
        await _bed.CreateAsync("Beta text", "Bob");
        await _bed.CreateAsync("Alpha text", "Alice");
        await _bed.CreateAsync("Gamma text", "alice");
        await _bed.CreateAsync("Number text", "42");
    }

    [Fact]
    public async Task List_OrdersByAuthorThenText()
    {
        await SeedAsync();

        var listed = await _bed.ListAsync();

        Assert.Equal(["Number text", "Alpha text", "Gamma text", "Beta text"], listed.Select(quote => quote.Text));
    }

    [Fact]
    public async Task List_ShowsOnlyTheLatestVersionOfEveryQuote()
    {
        var key = await _bed.CreateAsync("Old", "Ada");
        await _bed.WithCommandsAsync(commands => commands.Update(key, new IQuoteCommands.QuoteDraft("New", "Ada")));

        var listed = await _bed.ListAsync();

        var quote = Assert.Single(listed);
        Assert.Equal("New", quote.Text);
        Assert.Equal(new VersionedKey<Guid>(key, 2), quote.Id);
    }

    [Fact]
    public async Task List_SearchesTheTextAndTheAuthorIgnoringCase()
    {
        await SeedAsync();

        Assert.Equal(["Beta text"], (await _bed.ListAsync(search: Some("BETA"))).Select(quote => quote.Text));
        Assert.Equal(["Bob"], (await _bed.ListAsync(search: Some(" bob "))).Select(quote => quote.Author));
        Assert.Empty(await _bed.ListAsync(search: Some("missing")));
    }

    [Fact]
    public async Task List_BlankSearchMatchesEverything()
    {
        await SeedAsync();

        Assert.Equal(4, (await _bed.ListAsync(search: Some("   "))).Count);
    }

    [Fact]
    public async Task List_FiltersByTheLetterOfTheAuthor()
    {
        await SeedAsync();

        Assert.Equal(["Alice", "alice"], (await _bed.ListAsync(letter: Some("a"))).Select(quote => quote.Author));
        Assert.Equal(["42"], (await _bed.ListAsync(letter: Some("#"))).Select(quote => quote.Author));
    }

    [Fact]
    public async Task List_PagesTheResults()
    {
        await SeedAsync();

        var page = await _bed.WithQueriesAsync(queries =>
            queries.List(new Flipper(2, 3), None, None, DeletionState.Active));

        Assert.Equal(4, page.Total);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageCount);
        Assert.Equal(3, page.PageSize);
        Assert.Equal(["Bob"], page.Results.Match(() => [], items => items.Select(quote => quote.Author).ToList()));
    }

    [Fact]
    public async Task List_WithoutQuotesHasNoResults()
    {
        var page = await _bed.WithQueriesAsync(queries =>
            queries.List(new Flipper(1, 10), None, None, DeletionState.Active));

        Assert.Equal(0, page.Total);
        Assert.Equal(None, page.Results);
    }

    [Fact]
    public async Task List_SeparatesActiveAndDeletedQuotes()
    {
        var kept = await _bed.CreateAsync("Kept", "Ada");
        var removed = await _bed.CreateAsync("Removed", "Ada");
        await _bed.WithCommandsAsync(commands => commands.Delete(removed));

        Assert.Equal([kept], (await _bed.ListAsync()).Select(quote => quote.Id.Key));
        Assert.Equal([removed], (await _bed.ListAsync(DeletionState.Deleted)).Select(quote => quote.Id.Key));
    }

    [Fact]
    public async Task GetLetters_ListsTheLettersOfTheMatchingAuthors()
    {
        await SeedAsync();

        var letters = await _bed.WithQueriesAsync(queries => queries.GetLetters(None, DeletionState.Active));

        Assert.Equal(["A", "B", "#"], letters);
    }

    [Fact]
    public async Task GetLetters_FollowsTheSearchAndTheDeletionState()
    {
        await SeedAsync();
        var removed = await _bed.CreateAsync("Zed text", "Zed");
        await _bed.WithCommandsAsync(commands => commands.Delete(removed));

        Assert.Equal(
            ["B"],
            await _bed.WithQueriesAsync(queries => queries.GetLetters(Some("bob"), DeletionState.Active)));
        Assert.Equal(
            ["Z"],
            await _bed.WithQueriesAsync(queries => queries.GetLetters(None, DeletionState.Deleted)));
    }
}

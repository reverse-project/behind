using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class NewsCommentQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<INewsCommentQueries.NewsCommentListing>> ListAsync(
        Guid newsId,
        Flipper flipper,
        Option<DateTimeOffset> activeAt = default)
    {
        await using var ctx = _bed.Read();

        return await new NewsCommentQueries(ctx).List(newsId, flipper, activeAt);
    }

    private static IReadOnlyList<string> Contents(PagedResult<INewsCommentQueries.NewsCommentListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Content).ToList());

    [Fact]
    public async Task List_ReturnsTheCommentsOfTheNewsOldestFirst()
    {
        var newsId = await _bed.CreateNewsAsync();
        var otherNews = await _bed.CreateNewsAsync("Other");
        await _bed.AddCommentAsync(newsId, "second", at: NewsTestBed.Now.AddMinutes(2));
        await _bed.AddCommentAsync(newsId, "first", at: NewsTestBed.Now.AddMinutes(1));
        await _bed.AddCommentAsync(otherNews, "elsewhere");

        var page = await ListAsync(newsId, new Flipper(1, 10));

        Assert.Equal(["first", "second"], Contents(page));
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task List_IsPaginated()
    {
        var newsId = await _bed.CreateNewsAsync();

        foreach (var index in Enumerable.Range(1, 5))
        {
            await _bed.AddCommentAsync(newsId, $"comment {index}", at: NewsTestBed.Now.AddMinutes(index));
        }

        Assert.Equal(["comment 1", "comment 2"], Contents(await ListAsync(newsId, new Flipper(1, 2))));
        Assert.Equal(["comment 5"], Contents(await ListAsync(newsId, new Flipper(3, 2))));
    }

    [Fact]
    public async Task List_CarriesTheAuthorAndTheCreationTime()
    {
        var newsId = await _bed.CreateNewsAsync();
        var key = await _bed.AddCommentAsync(newsId, "Hello", _bed.OtherAuthorId);

        var comment = Assert.Single((await ListAsync(newsId, new Flipper(1, 10))).Results.Match(() => [], items => items.ToList()));

        Assert.Equal(
            (new VersionedKey<Guid>(key, 1), _bed.OtherAuthorId, _bed.OtherAuthorId.ToUserLabel("Test User"), "Hello", Some(NewsTestBed.Now), None),
            (comment.Id, comment.AuthorId, comment.Author, comment.Content, comment.CreatedAt, comment.UpdatedAt));
    }

    [Fact]
    public async Task List_ShowsTheLatestVersionOfAnEditedCommentWithItsEditTimeAndKeepsItsPlace()
    {
        var newsId = await _bed.CreateNewsAsync();
        var edited = await _bed.AddCommentAsync(newsId, "edited", at: NewsTestBed.Now.AddMinutes(1));
        await _bed.AddCommentAsync(newsId, "untouched", at: NewsTestBed.Now.AddMinutes(2));
        await using (var ctx = _bed.Write(NewsTestBed.Now.AddHours(1)))
        {
            await new NewsCommentCommands(ctx, TimeProvider.System).Update(edited, "edited!");
        }

        var page = await ListAsync(newsId, new Flipper(1, 10));

        var comments = page.Results.Match(() => [], items => items.ToList());
        Assert.Equal(["edited!", "untouched"], comments.Select(comment => comment.Content));
        Assert.Equal(2, page.Total);
        Assert.Equal(
            [(Some(NewsTestBed.Now.AddMinutes(1)), Some(NewsTestBed.Now.AddHours(1))), (Some(NewsTestBed.Now.AddMinutes(2)), None)],
            comments.Select(comment => (comment.CreatedAt, comment.UpdatedAt)));
        Assert.Equal(new VersionedKey<Guid>(edited, 2), comments[0].Id);
    }

    [Fact]
    public async Task List_HidesDeletedComments()
    {
        var newsId = await _bed.CreateNewsAsync();
        var gone = await _bed.AddCommentAsync(newsId, "gone");
        await _bed.AddCommentAsync(newsId, "kept");
        await using (var ctx = _bed.Write())
        {
            await new NewsCommentCommands(ctx, TimeProvider.System).Delete(gone);
        }

        Assert.Equal(["kept"], Contents(await ListAsync(newsId, new Flipper(1, 10))));
    }

    [Fact]
    public async Task List_NoComments_ReturnsAnEmptyPage()
    {
        var page = await ListAsync(await _bed.CreateNewsAsync(), new Flipper(1, 10));

        Assert.Equal((0L, None), (page.Total, page.Results));
    }

    [Fact]
    public async Task List_WithAnActiveAtTime_OnlyListsCommentsOfNewsThatIsShown()
    {
        var now = NewsTestBed.Now;
        var live = await _bed.CreateNewsAsync("Live", startAt: now.AddDays(-1));
        var expiresLater = await _bed.CreateNewsAsync("Expires", startAt: now.AddDays(-1), endAt: now.AddDays(5));
        var draft = await _bed.CreateNewsAsync("Draft", publish: false);
        await _bed.AddCommentAsync(live, "on live");
        await _bed.AddCommentAsync(expiresLater, "on expiring");

        var afterExpiry = now.AddDays(6);

        Assert.Equal(["on expiring"], Contents(await ListAsync(expiresLater, new Flipper(1, 10), now)));
        Assert.Equal(["on live"], Contents(await ListAsync(live, new Flipper(1, 10), afterExpiry)));
        var hidden = await ListAsync(expiresLater, new Flipper(1, 10), afterExpiry);
        Assert.Empty(Contents(hidden));
        Assert.Equal(0, hidden.Total);
        Assert.Empty(Contents(await ListAsync(draft, new Flipper(1, 10), now)));
    }

    [Fact]
    public async Task List_WithoutAnActiveAtTime_ListsCommentsOfAnyNews()
    {
        var now = NewsTestBed.Now;
        var expiresLater = await _bed.CreateNewsAsync("Expires", startAt: now.AddDays(-1), endAt: now.AddDays(5));
        await _bed.AddCommentAsync(expiresLater, "on expiring");

        Assert.Equal(["on expiring"], Contents(await ListAsync(expiresLater, new Flipper(1, 10))));
    }

    [Fact]
    public async Task FindAuthor_ReturnsTheAuthorOfAnActiveComment()
    {
        var id = await _bed.AddCommentAsync(await _bed.CreateNewsAsync(), "Hi", _bed.OtherAuthorId);
        await using var ctx = _bed.Read();

        Assert.Equal(Some(_bed.OtherAuthorId), await new NewsCommentQueries(ctx).FindAuthor(id));
    }

    [Fact]
    public async Task FindAuthor_UnknownOrDeletedComment_ReturnsNone()
    {
        var id = await _bed.AddCommentAsync(await _bed.CreateNewsAsync());
        await using (var ctx = _bed.Write())
        {
            await new NewsCommentCommands(ctx, TimeProvider.System).Delete(id);
        }

        await using var read = _bed.Read();
        var queries = new NewsCommentQueries(read);

        Assert.Equal(None, await queries.FindAuthor(id));
        Assert.Equal(None, await queries.FindAuthor(Guid.NewGuid()));
    }
}

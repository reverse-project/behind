using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class NewsCommentCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<T> CommandsAsync<T>(Func<NewsCommentCommands, Task<T>> action, DateTimeOffset? at = null)
    {
        await using var ctx = _bed.Write(at);

        return await action(new NewsCommentCommands(ctx, new FixedTimeProvider(at ?? NewsTestBed.Now)));
    }

    private async Task<IReadOnlyList<(long Version, string Content, Guid News, Guid Author)>> VersionsAsync(Guid key)
    {
        await using var read = _bed.Read();
        var rows = await read.NewsComments
            .OfKey(key)
            .OrderBy(comment => EF.Property<long>(comment, VersionedColumns.Version))
            .Select(comment => new
            {
                Version = EF.Property<long>(comment, VersionedColumns.Version),
                comment.Content,
                News = EF.Property<Guid>(comment, NewsCommentConfiguration.NewsIdProperty),
                Author = EF.Property<Guid>(comment, NewsCommentConfiguration.AuthorIdProperty)
            })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Content, row.News, row.Author)).ToList();
    }

    [Fact]
    public async Task Add_StoresVersionOneWithItsNewsAuthorAndTime()
    {
        var newsId = await _bed.CreateNewsAsync();

        var id = await _bed.AddCommentAsync(newsId, "Great", _bed.OtherAuthorId);

        Assert.Equal([(1L, "Great", newsId, _bed.OtherAuthorId)], await VersionsAsync(id));
        Assert.Equal(["Created"], await _bed.ActionsAsync("NewsComment", id));
        Assert.Equal(Some(NewsTestBed.Now), (await _bed.TimesAsync<NewsComment>(id, 1)).CreatedAt);
    }

    [Fact]
    public async Task Add_UnknownDraftOrDeletedNews_ReturnsNone()
    {
        var draft = await _bed.CreateNewsAsync(publish: false);
        var deleted = await _bed.CreateNewsAsync();
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(deleted));

        foreach (var newsId in new[] { Guid.NewGuid(), draft, deleted })
        {
            Assert.Equal(None, await CommandsAsync(commands => commands.Add(newsId, _bed.AuthorId, "Hi")));
        }
    }

    [Fact]
    public async Task Add_NewsThatHasNotStartedOrHasEnded_ReturnsNone()
    {
        var scheduled = await _bed.CreateNewsAsync("Scheduled", startAt: NewsTestBed.Now.AddDays(1));
        var expired = await _bed.CreateNewsAsync("Expired", startAt: NewsTestBed.Now.AddDays(-3), endAt: NewsTestBed.Now.AddDays(-1));

        Assert.Equal(None, await CommandsAsync(commands => commands.Add(scheduled, _bed.AuthorId, "early")));
        Assert.Equal(None, await CommandsAsync(commands => commands.Add(expired, _bed.AuthorId, "late")));
    }

    [Fact]
    public async Task Add_NewsThatBecomesLive_IsAccepted()
    {
        var scheduled = await _bed.CreateNewsAsync("Scheduled", startAt: NewsTestBed.Now.AddDays(1));

        var added = await CommandsAsync(
            commands => commands.Add(scheduled, _bed.AuthorId, "on time"),
            NewsTestBed.Now.AddDays(1));

        Assert.NotEqual(None, added);
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionKeepingTheOldOneTheNewsAndTheAuthor()
    {
        var newsId = await _bed.CreateNewsAsync();
        var id = await _bed.AddCommentAsync(newsId, "typo", _bed.OtherAuthorId);

        Assert.True(await CommandsAsync(commands => commands.Update(id, "fixed"), NewsTestBed.Now.AddHours(1)));

        Assert.Equal(
            [(1L, "typo", newsId, _bed.OtherAuthorId), (2L, "fixed", newsId, _bed.OtherAuthorId)],
            await VersionsAsync(id));
        Assert.Equal(["Created"], await _bed.ActionsAsync("NewsComment", id, 2));
        Assert.Equal(
            new AuditTimes(Some(NewsTestBed.Now), Some(NewsTestBed.Now.AddHours(1))),
            await _bed.TimesAsync<NewsComment>(id, 2));
    }

    [Fact]
    public async Task Update_UnknownOrDeletedComment_ReturnsFalse()
    {
        var id = await _bed.AddCommentAsync(await _bed.CreateNewsAsync(), "gone");
        await CommandsAsync(commands => commands.Delete(id));

        Assert.False(await CommandsAsync(commands => commands.Update(id, "too late")));
        Assert.False(await CommandsAsync(commands => commands.Update(Guid.NewGuid(), "nothing")));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersionOfTheComment()
    {
        var id = await _bed.AddCommentAsync(await _bed.CreateNewsAsync());
        await CommandsAsync(commands => commands.Update(id, "edited"));

        Assert.True(await CommandsAsync(commands => commands.Delete(id)));

        await using var read = _bed.Read();
        Assert.Empty(await read.NewsComments.ToListAsync());
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("NewsComment", id, 1));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("NewsComment", id, 2));
    }

    [Fact]
    public async Task Delete_UnknownOrAlreadyDeletedComment_ReturnsFalse()
    {
        var id = await _bed.AddCommentAsync(await _bed.CreateNewsAsync());
        await CommandsAsync(commands => commands.Delete(id));

        Assert.False(await CommandsAsync(commands => commands.Delete(id)));
        Assert.False(await CommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Add_UnscheduledNews_IsAcceptedAtAnyTime()
    {
        var newsId = await _bed.CreateNewsAsync("Plain");

        var added = await CommandsAsync(commands => commands.Add(newsId, _bed.AuthorId, "much later"), NewsTestBed.Now.AddYears(10));

        Assert.NotEqual(None, added);
    }
}

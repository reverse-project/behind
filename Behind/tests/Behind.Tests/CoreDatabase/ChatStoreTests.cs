using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Behind.Tests.CoreDatabase;

public sealed class ChatStoreTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    [Fact]
    public async Task History_PagesAllMessagesByAuditCreationWithStableTiesAndConcurrentInserts()
    {
        var ids = new List<Guid>();
        for (var index = 0; index < 205; index++)
        {
            ids.Add(await Add($"message {index}", NewsTestBed.Now.AddSeconds(index / 2)));
        }

        await using var context = _bed.Read();
        var store = Store(context);
        var first = (await store.List(null, 50, default))!;
        Assert.Equal(50, first.Items.Count);
        await Add("new arrival", NewsTestBed.Now.AddHours(1));
        var all = first.Items.ToList();
        var cursor = first.NextCursor;
        while (cursor is not null)
        {
            var page = (await store.List(cursor, 50, default))!;
            all.AddRange(page.Items);
            cursor = page.NextCursor;
        }

        Assert.Equal(205, all.Count);
        Assert.Equal(205, all.Select(message => message.Id).Distinct().Count());
        Assert.Equal(ids.Order(), all.Select(message => message.Id).Order());
        Assert.Equal(NewsTestBed.Now.AddSeconds(102), all[0].CreatedAt);
        Assert.Null(await store.List(Guid.NewGuid(), 20, default));
    }

    [Fact]
    public async Task Edits_InsertVersionsAndUseAuditTimesWithoutRestartingTheWindow()
    {
        var id = await Add("original", NewsTestBed.Now);
        await using (var context = _bed.Write(NewsTestBed.Now.AddMinutes(4)))
        {
            Assert.Equal(IChatStore.SaveStatus.Saved,
                (await Store(context, NewsTestBed.Now.AddMinutes(4)).Update(id, 1, _bed.AuthorId, "edited", default)).Status);
        }

        await using var read = _bed.Read();
        var message = Assert.Single(await Store(read).Find([id], default));
        Assert.Equal(2, message.Version);
        Assert.Equal("edited", message.Text);
        Assert.Equal(NewsTestBed.Now, message.CreatedAt);
        Assert.Equal(NewsTestBed.Now.AddMinutes(4), message.UpdatedAt);
        Assert.Equal(2, await read.ChatMessages.OfKey(id).CountAsync());
        var labels = await read.ResolveAuditLabelsAsync([id], []);
        Assert.Equal("Chat message of Test User", labels.Label(id.ToString()));
    }

    [Theory]
    [InlineData(299999, true)]
    [InlineData(300000, false)]
    [InlineData(300001, false)]
    public async Task Edits_EnforceExactFiveMinuteBoundary(int milliseconds, bool allowed)
    {
        var id = await Add("original", NewsTestBed.Now);
        var now = NewsTestBed.Now.AddMilliseconds(milliseconds);
        await using var context = _bed.Write(now);
        var result = await Store(context, now).Update(id, 1, _bed.AuthorId, "edited", default);
        Assert.Equal(allowed ? IChatStore.SaveStatus.Saved : IChatStore.SaveStatus.Expired, result.Status);
    }

    [Fact]
    public async Task Edits_RejectOtherAuthorsStaleVersionsAndMissingMessages()
    {
        var id = await Add("original", NewsTestBed.Now);
        await using var context = _bed.Write();
        var store = Store(context);
        Assert.Equal(IChatStore.SaveStatus.Forbidden,
            (await store.Update(id, 1, _bed.OtherAuthorId, "other", default)).Status);
        Assert.Equal(IChatStore.SaveStatus.Conflict,
            (await store.Update(id, 2, _bed.AuthorId, "stale", default)).Status);
        Assert.Equal(IChatStore.SaveStatus.NotFound,
            (await store.Update(Guid.NewGuid(), 1, _bed.AuthorId, "missing", default)).Status);
    }

    [Fact]
    public async Task Moderation_HidesAllVersionsAndReplyPreviewsButKeepsCursorUsable()
    {
        var parent = await Add("parent", NewsTestBed.Now);
        Guid reply;
        await using (var context = _bed.Write(NewsTestBed.Now.AddSeconds(1)))
        {
            var result = await Store(context).Add(_bed.OtherAuthorId, "reply", parent, default);
            reply = result.Id!.Value;
        }

        await using (var context = _bed.Write(NewsTestBed.Now.AddMinutes(1)))
        {
            var store = Store(context, NewsTestBed.Now.AddMinutes(1));
            await store.Update(parent, 1, _bed.AuthorId, "changed", default);
            Assert.True(await store.Delete(parent, default));
            Assert.False(await store.Delete(parent, default));
            Assert.Equal(IChatStore.SaveStatus.NotFound,
                (await store.Update(parent, 2, _bed.AuthorId, "hidden", default)).Status);
            Assert.Equal(IChatStore.SaveStatus.InvalidReply,
                (await store.Add(_bed.AuthorId, "bad reply", parent, default)).Status);
        }

        await using var read = _bed.Read();
        var reader = Store(read);
        var page = (await reader.List(null, 20, default))!;
        Assert.Equal(reply, Assert.Single(page.Items).Id);
        Assert.Empty(await reader.Find([parent], default));
        Assert.NotNull(await reader.List(parent, 20, default));
        Assert.Equal(2, await read.ChatMessages.IgnoreQueryFilters().OfKey(parent).CountAsync());
    }

    [Fact]
    public async Task Reply_RequiresAnExistingVisibleParent()
    {
        await using var context = _bed.Write();
        Assert.Equal(IChatStore.SaveStatus.InvalidReply,
            (await Store(context).Add(_bed.AuthorId, "reply", Guid.NewGuid(), default)).Status);
    }

    private async Task<Guid> Add(string text, DateTimeOffset at)
    {
        await using var context = _bed.Write(at);
        var result = await Store(context, at).Add(_bed.AuthorId, text, null, default);
        Assert.Equal(IChatStore.SaveStatus.Saved, result.Status);
        return result.Id!.Value;
    }

    private static ChatStore Store(AbstractCoreContext context, DateTimeOffset? now = null) =>
        new(context, new FixedTimeProvider(now ?? NewsTestBed.Now), NullLogger<ChatStore>.Instance);

    public void Dispose() => _bed.Dispose();
}

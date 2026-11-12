using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
            all.InsertRange(0, page.Items);
            cursor = page.NextCursor;
        }

        Assert.Equal(205, all.Count);
        Assert.Equal(205, all.Select(message => message.Id).Distinct().Count());
        Assert.Equal(ids.Order(), all.Select(message => message.Id).Order());
        Assert.Equal(NewsTestBed.Now, all[0].CreatedAt);
        Assert.Equal(NewsTestBed.Now.AddSeconds(102), all[^1].CreatedAt);
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
        Assert.Equal($"Chat message of {_bed.AuthorId.ToUserLabel("Test User")}", labels.Label(id.ToString()));
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

    [Fact]
    public async Task Anonymity_IsVersionedWithoutChangingInternalOwnership()
    {
        Guid id;
        await using (var context = _bed.Write())
        {
            id = (await Store(context).Add(_bed.AuthorId, "hidden", null, default, true)).Id!.Value;
        }
        await using (var context = _bed.Read())
        {
            var message = Assert.Single(await Store(context).Find([id], default));
            Assert.True(message.IsAnonymous);
            Assert.Equal(_bed.AuthorId, message.AuthorId);
        }
        await using (var context = _bed.Write(NewsTestBed.Now.AddMinutes(1)))
        {
            Assert.Equal(IChatStore.SaveStatus.Forbidden,
                (await Store(context).Update(id, 1, _bed.OtherAuthorId, "stolen", default, true)).Status);
            Assert.Equal(IChatStore.SaveStatus.Saved,
                (await Store(context).Update(id, 1, _bed.AuthorId, "still hidden", default)).Status);
        }
        await using (var context = _bed.Read())
        {
            var message = Assert.Single((await Store(context).List(null, 20, default))!.Items);
            Assert.True(message.IsAnonymous);
            Assert.Equal(2, message.Version);
        }
        await using (var context = _bed.Write(NewsTestBed.Now.AddMinutes(2)))
        {
            Assert.Equal(IChatStore.SaveStatus.Saved,
                (await Store(context).Update(id, 2, _bed.AuthorId, "public", default, false)).Status);
        }
        await using (var context = _bed.Read())
        {
            var message = Assert.Single(await Store(context).Find([id], default));
            Assert.False(message.IsAnonymous);
            Assert.Equal(_bed.AuthorId, message.AuthorId);
            Assert.Equal(3, message.Version);
            Assert.True((await context.ChatMessages.OfKey(id)
                .Where(row => EF.Property<long>(row, VersionedColumns.Version) == 1).SingleAsync()).IsAnonymous);
        }
    }

    [Fact]
    public async Task LegacyEdit_PreservesAnonymityInHistoryAndNestedReplyUntilExplicitOptOut()
    {
        await using var context = _bed.Write();
        var store = Store(context);
        var parent = (await store.Add(_bed.AuthorId, "hidden parent", null, default, true)).Id!.Value;
        await store.Add(_bed.OtherAuthorId, "reply", parent, default);
        Assert.Equal(IChatStore.SaveStatus.Saved,
            (await store.Update(parent, 1, _bed.AuthorId, "legacy edited", default)).Status);
        var http = new DefaultHttpContext();
        var identity = Substitute.For<IModuleIdentity>();
        var users = Substitute.For<IModuleUsers>();
        users.Find(default!, default).ReturnsForAnyArgs(
            new[] { new IModuleUsers.Profile(_bed.AuthorId, "Real author", Guid.NewGuid()) });
        var authorization = new FakeAuthorizationService();

        async Task<ChatEndpoints.Page> Response() =>
            Assert.IsType<Ok<ChatEndpoints.Page>>(await ChatEndpoints.List(
                http, store, identity, users, authorization, new FixedTimeProvider(NewsTestBed.Now), default)).Value!;

        var hidden = await Response();
        var message = hidden.Items.Single(item => item.Id == parent);
        var preview = hidden.Items.Single(item => item.ReplyToId == parent).Reply!;
        Assert.True(message.IsAnonymous);
        Assert.Null(message.AuthorId);
        Assert.Null(message.ProfilePictureUrl);
        Assert.Equal("", message.Author);
        Assert.True(preview.IsAnonymous);
        Assert.Null(preview.AuthorId);
        Assert.Equal("", preview.Author);
        await store.Update(parent, 2, _bed.AuthorId, "public", default, false);
        var visible = await Response();
        Assert.Equal(_bed.AuthorId, visible.Items.Single(item => item.Id == parent).AuthorId);
        Assert.Equal(_bed.AuthorId, visible.Items.Single(item => item.ReplyToId == parent).Reply!.AuthorId);
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

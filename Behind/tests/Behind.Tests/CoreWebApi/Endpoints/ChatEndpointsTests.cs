using System.Security.Claims;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class ChatEndpointsTests
{
    private readonly DefaultHttpContext _http = new();
    private readonly IChatStore _store = Substitute.For<IChatStore>();
    private readonly IModuleIdentity _identity = Substitute.For<IModuleIdentity>();
    private readonly IModuleUsers _users = Substitute.For<IModuleUsers>();
    private readonly IAuthorizationService _authorization = Substitute.For<IAuthorizationService>();
    private readonly TimeProvider _clock = new FixedTimeProvider(NewsTestBed.Now);

    public ChatEndpointsTests()
    {
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Failed());
        Permit(ChatEndpoints.ReadGrant);
        Permit(ChatEndpoints.WriteGrant);
    }

    [Fact]
    public async Task Mutations_RequireIdentityAndValidateTextAndVersion()
    {
        Assert.IsType<UnauthorizedHttpResult>(await Add("hello"));
        Assert.IsType<UnauthorizedHttpResult>(await Update("hello", 1));
        Assert.IsType<UnauthorizedHttpResult>(await Delete());
        _identity.UserId(_http).Returns(Guid.NewGuid());
        Assert.IsType<BadRequest>(await Add(" "));
        Assert.IsType<BadRequest>(await Add(new string('x', 1001)));
        Assert.IsType<BadRequest>(await Update(" ", 1));
        Assert.IsType<BadRequest>(await Update("hello", 0));
        Assert.IsType<BadRequest>(await Update(new string('x', 1001), 1));
        await _store.DidNotReceiveWithAnyArgs().Add(default, "", default, default);
        await _store.DidNotReceiveWithAnyArgs().Update(default, default, default, "", default);
    }

    [Fact]
    public async Task Add_TrimsTextAndPassesAuthenticatedAuthorAndReply()
    {
        var user = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var id = Guid.NewGuid();
        _identity.UserId(_http).Returns(user);
        _store.Add(user, "hello", parent, default).Returns(new IChatStore.SaveResult(IChatStore.SaveStatus.Saved, id));

        var result = await ChatEndpoints.Add(new(" hello ", parent), _http, _store, _identity, _authorization, default);
        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        await _store.Received().Add(user, "hello", parent, default);
    }

    [Theory]
    [InlineData(IChatStore.SaveStatus.NotFound, 404)]
    [InlineData(IChatStore.SaveStatus.Forbidden, 403)]
    [InlineData(IChatStore.SaveStatus.Expired, 403)]
    [InlineData(IChatStore.SaveStatus.Conflict, 409)]
    public async Task Update_MapsStoreFailures(IChatStore.SaveStatus status, int expected)
    {
        _identity.UserId(_http).Returns(Guid.NewGuid());
        _store.Update(Arg.Any<Guid>(), 1, Arg.Any<Guid>(), "hello", default).Returns(new IChatStore.SaveResult(status));
        Assert.Equal(expected, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await Update("hello", 1)).StatusCode);
    }

    [Fact]
    public async Task Delete_RequiresDedicatedPermissionEvenForAnAuthenticatedAuthor()
    {
        _identity.UserId(_http).Returns(Guid.NewGuid());
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), ChatEndpoints.ModerationGrant)
            .Returns(AuthorizationResult.Failed());
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(await Delete()).StatusCode);
        await _store.DidNotReceiveWithAnyArgs().Delete(default, default);
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), ChatEndpoints.ModerationGrant)
            .Returns(AuthorizationResult.Success());
        _store.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([
            new(Guid.NewGuid(), 1, Guid.NewGuid(), "message", null, NewsTestBed.Now, null)
        ]);
        _store.Delete(Arg.Any<Guid>(), default).Returns(true);
        Assert.IsType<NoContent>(await Delete());
        _store.Delete(Arg.Any<Guid>(), default).Returns(false);
        Assert.IsType<NotFound>(await Delete());
    }

    [Fact]
    public async Task List_IncludesProfilesAuditTimesRepliesAndServerCalculatedActions()
    {
        var user = Guid.NewGuid();
        var picture = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var id = Guid.NewGuid();
        _identity.UserId(_http).Returns(user);
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), ChatEndpoints.ModerationGrant)
            .Returns(AuthorizationResult.Success());
        _store.List(null, 20, default).Returns(new IChatStore.Page([
            new(id, 2, user, "<script>", parent, NewsTestBed.Now.AddMinutes(-4), NewsTestBed.Now.AddMinutes(-1))
        ], parent));
        _store.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([
            new(parent, 1, user, "parent", null, NewsTestBed.Now.AddMinutes(-5), null)
        ]);
        _users.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([new IModuleUsers.Profile(user, "Ada", picture)]);

        var response = Assert.IsType<Ok<ChatEndpoints.Page>>(await List());
        var message = Assert.Single(response.Value!.Items);
        Assert.Equal("Ada", message.Author);
        Assert.Equal($"/profile/images/{picture}", message.ProfilePictureUrl);
        Assert.Equal("<script>", message.Text);
        Assert.Equal(NewsTestBed.Now.AddMinutes(-4), message.CreatedAt);
        Assert.Equal(NewsTestBed.Now.AddMinutes(-1), message.UpdatedAt);
        Assert.Equal(NewsTestBed.Now.AddMinutes(1), message.EditableUntil);
        Assert.True(message.CanEdit);
        Assert.True(message.CanDelete);
        Assert.Equal("parent", message.Reply!.Text);
        Assert.Equal(parent, response.Value.NextCursor);
        Assert.Equal("no-store", _http.Response.Headers.CacheControl);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AnonymousHistoryAndReplyPreviews_OnlyRevealIdentityToModerators(bool owner, bool moderator)
    {
        var author = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var id = Guid.NewGuid();
        _identity.UserId(_http).Returns(owner ? author : moderator ? Guid.NewGuid() : null);
        if (moderator)
        {
            Permit(ChatEndpoints.ModerationGrant);
        }
        Permit(ChatEndpoints.DeleteGrant);
        _store.List(null, 20, default).Returns(new IChatStore.Page([
            new(id, 1, author, "anonymous", parent, NewsTestBed.Now, null, true)
        ], null));
        _store.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([
            new(parent, 1, author, "anonymous parent", null, NewsTestBed.Now, null, true)
        ]);
        _users.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns([new IModuleUsers.Profile(author, "Real author", Guid.NewGuid())]);

        var message = Assert.Single(Assert.IsType<Ok<ChatEndpoints.Page>>(await List()).Value!.Items);
        var details = Assert.IsType<Ok<ChatEndpoints.Message>>(await ChatEndpoints.Get(
            id, _http, _store, _identity, _users, _authorization, _clock, default)).Value!;
        foreach (var item in new[] { message, details })
        {
            Assert.True(item.IsAnonymous);
            Assert.Equal(moderator ? author : (Guid?)null, item.AuthorId);
            Assert.Equal(moderator ? "Real author" : "", item.Author);
            Assert.Equal(moderator, item.ProfilePictureUrl is not null);
        }
        Assert.True(message.Reply!.IsAnonymous);
        Assert.Equal(moderator ? author : (Guid?)null, message.Reply.AuthorId);
        Assert.Equal(moderator ? "Real author" : "", message.Reply.Author);
        Assert.Equal(owner, message.CanEdit);
        Assert.Equal(owner || moderator, message.CanDelete);
    }

    [Fact]
    public async Task AnonymousMutations_ForwardTheFlagWithAuthenticatedAuthor()
    {
        var author = Guid.NewGuid();
        var id = Guid.NewGuid();
        _identity.UserId(_http).Returns(author);
        _store.Add(author, "hidden", null, default, true).Returns(new IChatStore.SaveResult(IChatStore.SaveStatus.Saved, id));
        _store.Update(id, 1, author, "edited", default, true).Returns(new IChatStore.SaveResult(IChatStore.SaveStatus.Saved, id));

        await ChatEndpoints.Add(new("hidden", IsAnonymous: true), _http, _store, _identity, _authorization, default);
        await ChatEndpoints.Update(id, new("edited", 1, true), _http, _store, _identity, _authorization, default);

        await _store.Received().Add(author, "hidden", null, default, true);
        await _store.Received().Update(id, 1, author, "edited", default, true);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    [InlineData(true, 6)]
    public async Task List_DoesNotAllowOtherAuthorsOrExpiredEdits(bool own, int age)
    {
        var user = Guid.NewGuid();
        _identity.UserId(_http).Returns(own ? user : Guid.NewGuid());
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), ChatEndpoints.ModerationGrant)
            .Returns(AuthorizationResult.Failed());
        _store.List(null, 20, default).Returns(new IChatStore.Page([
            new(Guid.NewGuid(), 1, user, "text", Guid.NewGuid(), NewsTestBed.Now.AddMinutes(-age), null)
        ], null));
        _store.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([]);
        _users.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([]);
        var message = Assert.Single(Assert.IsType<Ok<ChatEndpoints.Page>>(await List()).Value!.Items);
        Assert.False(message.CanEdit);
        Assert.False(message.CanDelete);
        Assert.Null(message.Reply);
    }

    [Fact]
    public async Task List_RejectsInvalidPageSizesAndUnknownCursors()
    {
        Assert.IsType<BadRequest>(await List(0));
        Assert.IsType<BadRequest>(await List(51));
        Assert.IsType<BadRequest>(await List());
    }

    [Theory]
    [InlineData("Chat.Read")]
    [InlineData("Chat.Write")]
    public async Task Write_RejectsMissingReadOrWriteGrant(string missing)
    {
        _identity.UserId(_http).Returns(Guid.NewGuid());
        Deny(missing);
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(await Add("hello")).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(await Update("hello", 1)).StatusCode);
        await _store.DidNotReceiveWithAnyArgs().Add(default, "", default, default);
        await _store.DidNotReceiveWithAnyArgs().Update(default, default, default, "", default);
    }

    [Fact]
    public async Task Read_RejectsRestrictedSignedInUsersBeforeQueryingHistoryOrIndividualMessages()
    {
        _identity.UserId(_http).Returns(Guid.NewGuid());
        Deny(ChatEndpoints.ReadGrant);
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(await List()).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(await ChatEndpoints.Get(
            Guid.NewGuid(), _http, _store, _identity, _users, _authorization, _clock, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(await Delete()).StatusCode);
        await _store.DidNotReceiveWithAnyArgs().List(default, default, default);
        await _store.DidNotReceiveWithAnyArgs().Find([], default);
    }

    [Fact]
    public async Task Guests_CanReadButCannotWriteOrReceiveMutationActions()
    {
        Deny(ChatEndpoints.ReadGrant);
        var message = new IChatStore.Message(Guid.NewGuid(), 1, Guid.NewGuid(), "public", null, NewsTestBed.Now, null);
        _store.List(null, 20, default).Returns(new IChatStore.Page([message], null));
        _store.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([]);
        _users.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([]);
        var page = Assert.IsType<Ok<ChatEndpoints.Page>>(await List()).Value!;
        Assert.False(page.CanWrite);
        Assert.False(page.Items[0].CanEdit);
        Assert.False(page.Items[0].CanDelete);
        Assert.IsType<UnauthorizedHttpResult>(await Add("hello"));
        Assert.IsType<UnauthorizedHttpResult>(await Update("hello", 1));
        Assert.IsType<UnauthorizedHttpResult>(await Delete());
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public async Task DeleteGrant_RemovesOnlyOwnWhileManageCanRemoveAny(bool own, bool manage, bool allowed)
    {
        var user = Guid.NewGuid();
        _identity.UserId(_http).Returns(user);
        Permit(ChatEndpoints.DeleteGrant);
        if (manage)
        {
            Permit(ChatEndpoints.ModerationGrant);
        }
        _store.Find(Arg.Any<IReadOnlyCollection<Guid>>(), default).Returns([
            new(Guid.NewGuid(), 1, own ? user : Guid.NewGuid(), "message", null, NewsTestBed.Now, null)
        ]);
        _store.Delete(Arg.Any<Guid>(), default).Returns(true);

        var result = await Delete();
        Assert.Equal(allowed ? 204 : 403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        if (!allowed)
        {
            await _store.DidNotReceiveWithAnyArgs().Delete(default, default);
        }
    }

    private void Permit(string grant) =>
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), grant).Returns(AuthorizationResult.Success());
    private void Deny(string grant) =>
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), grant).Returns(AuthorizationResult.Failed());
    private Task<IResult> Add(string text) => ChatEndpoints.Add(new(text), _http, _store, _identity, _authorization, default);
    private Task<IResult> Update(string text, long version) =>
        ChatEndpoints.Update(Guid.NewGuid(), new(text, version), _http, _store, _identity, _authorization, default);
    private Task<IResult> Delete() => ChatEndpoints.Delete(Guid.NewGuid(), _http, _store, _identity, _authorization, default);
    private Task<IResult> List(int limit = 20) =>
        ChatEndpoints.List(_http, _store, _identity, _users, _authorization, _clock, default, limit: limit);
}

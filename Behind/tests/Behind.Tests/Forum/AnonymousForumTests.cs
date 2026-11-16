using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Behind.Forum.Database.Commands;
using Behind.Forum.Domain;
using Behind.Forum.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Behind.Tests.Forum;

public sealed class AnonymousForumTests : IDisposable
{
    private readonly SportTestBed _bed = new();
    private readonly Guid _author = Guid.NewGuid();

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, ForumNames.Moderate)]
    [InlineData(false, ForumNames.Manage)]
    public async Task AllResponses_HideAnonymousProfilesExceptForAuthorizedModeration(bool owner, string grant)
    {
        await using var context = _bed.Write();
        var board = await Board(context);
        var commands = new ForumPostCommands(context);
        var topic = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ForumPost(board, null, _author, "Topic", "Message", PublicationState.Published, true))).Id.Key;
        var reply = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ForumPost(board, topic, _author, "", "Reply", PublicationState.Published, true))).Id.Key;
        var users = Substitute.For<IModuleUsers>();
        users.Find(default!, default).ReturnsForAnyArgs(
            new[] { new IModuleUsers.Profile(_author, "Real author", Guid.NewGuid()) });
        var http = new DefaultHttpContext();
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(http).Returns(owner ? _author : grant.Length > 0 ? Guid.NewGuid() : null);
        var authorization = new FakeAuthorizationService(grant);
        var queries = new ForumQueries(context);
        var topics = Assert.IsType<Ok<ForumEndpoints.PostPage>>(await ForumEndpoints.Posts(
            http, queries, users, board, null, 1, authorization, identity)).Value!;
        var replies = Assert.IsType<Ok<ForumEndpoints.PostPage>>(await ForumEndpoints.Posts(
            http, queries, users, board, topic, 1, authorization, identity)).Value!;
        var widget = Assert.IsType<Ok<ForumEndpoints.PostPage>>(await ForumEndpoints.LatestWidgetPosts(
            http, queries, users, new ModuleCreationOrder(context), 10, authorization, identity)).Value!;
        var details = Assert.IsType<Ok<ForumEndpoints.PostResponse>>(await ForumEndpoints.GetPost(
            reply, http, queries, users, authorization, identity)).Value!;
        var managed = Assert.IsType<Ok<ForumEndpoints.PostPage>>(await ForumEndpoints.ManagePosts(
            http, queries, users, authorization, 1, null)).Value!;
        var moderator = grant.Length > 0;

        foreach (var post in topics.Items.Concat(replies.Items).Concat(widget.Items).Append(details).Concat(managed.Items))
        {
            Assert.True(post.IsAnonymous);
            Assert.Equal(moderator ? _author : (Guid?)null, post.AuthorId);
            Assert.Equal(moderator ? "Real author" : "", post.Author);
            Assert.Equal(moderator, post.PictureUrl is not null);
        }
        Assert.Equal(owner || moderator, details.CanEdit);
        Assert.Equal(owner || moderator, details.CanDelete);
        Assert.Equal(ManagedListExtensions.NoStore, http.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task AnonymousSubmission_RequiresIdentityAndPersistsFlagAndRealOwner()
    {
        await using var context = _bed.Write();
        var board = await Board(context);
        var http = new DefaultHttpContext();
        var identity = Substitute.For<IModuleIdentity>();
        var settings = Substitute.For<IOptionsMonitor<ForumSettings>>();
        settings.CurrentValue.Returns(new ForumSettings());
        var queries = new ForumQueries(context);
        var commands = new ForumPostCommands(context);
        var request = new ForumEndpoints.PostRequest(board, null, "Topic", "Hidden", true);
        Assert.IsType<UnauthorizedHttpResult>(await ForumEndpoints.CreatePost(
            request, http, identity, queries, commands, settings));
        Assert.Empty(await context.ForumPosts.ToListAsync());
        identity.UserId(http).Returns(_author);

        var result = Assert.IsType<Created<ForumEndpoints.PostSavedResponse>>(await ForumEndpoints.CreatePost(
            request, http, identity, queries, commands, settings));
        var stored = (await queries.Post(result.Value!.Id, default))!.Entity;
        Assert.True(stored.IsAnonymous);
        Assert.Equal(_author, stored.AuthorId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousOwnership_AuthorCanEditAndDeleteWithoutRevealingTheirIdentity(bool reply)
    {
        await using var context = _bed.Write();
        var board = await Board(context);
        var commands = new ForumPostCommands(context);
        Guid? topic = reply ? Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ForumPost(board, null, _author, "Parent", "Parent", PublicationState.Published))).Id.Key : null;
        var id = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ForumPost(board, topic, _author, "Topic", "Hidden", PublicationState.Published, true))).Id.Key;
        var http = new DefaultHttpContext();
        var identity = Substitute.For<IModuleIdentity>();
        var queries = new ForumQueries(context);
        var authorization = new FakeAuthorizationService();
        var request = new ForumEndpoints.PostRequest(board, null, "Edited", "Public");
        identity.UserId(http).Returns(Guid.NewGuid());
        Assert.IsType<ForbidHttpResult>(await ForumEndpoints.UpdatePost(
            id, request, http, queries, commands, identity, authorization));
        Assert.IsType<ForbidHttpResult>(await ForumEndpoints.DeletePost(
            id, http, queries, commands, identity, authorization));
        identity.UserId(http).Returns(_author);
        Assert.IsType<JsonHttpResult<VersionedSavedResponse>>(await ForumEndpoints.UpdatePost(
            id, request, http, queries, commands, identity, authorization));
        var updated = (await queries.Post(id, default))!;
        Assert.True(updated.Entity.IsAnonymous);
        Assert.Equal(_author, updated.Entity.AuthorId);
        Assert.Equal(2, updated.Version);
        var users = Substitute.For<IModuleUsers>();
        users.Find(default!, default).ReturnsForAnyArgs(
            new[] { new IModuleUsers.Profile(_author, "Real author", null) });
        var hidden = Assert.IsType<Ok<ForumEndpoints.PostResponse>>(await ForumEndpoints.GetPost(
            id, http, queries, users, authorization, identity)).Value!;
        Assert.Null(hidden.AuthorId);
        Assert.IsType<JsonHttpResult<VersionedSavedResponse>>(await ForumEndpoints.UpdatePost(
            id, request with { IsAnonymous = false }, http, queries, commands, identity, authorization));
        var visible = Assert.IsType<Ok<ForumEndpoints.PostResponse>>(await ForumEndpoints.GetPost(
            id, http, queries, users, authorization, identity)).Value!;
        Assert.False(visible.IsAnonymous);
        Assert.Equal(_author, visible.AuthorId);
        Assert.True((await context.ForumPosts.OfKey(id)
            .Where(post => EF.Property<long>(post, VersionedColumns.Version) == 1).SingleAsync()).IsAnonymous);
        Assert.IsType<NoContent>(await ForumEndpoints.DeletePost(
            id, http, queries, commands, identity, authorization));
        Assert.Null(await queries.Post(id, default));
    }

    private static async Task<Guid> Board(IForumContext context) =>
        Assert.IsType<VersionedSaveResult.Saved>(await new ForumBoardCommands(context)
            .Create(new ForumBoard("General", "", null))).Id.Key;

    public void Dispose() => _bed.Dispose();
}

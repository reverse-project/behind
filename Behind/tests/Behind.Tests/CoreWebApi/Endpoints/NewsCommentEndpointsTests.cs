using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class NewsCommentEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);
    private static readonly Guid NewsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CommentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private static INewsCommentQueries.NewsCommentListing Comment(Guid authorId) =>
        new(new VersionedKey<Guid>(CommentId, 1), authorId, "Ada", "Nice", Now);

    private static INewsCommentQueries QueriesWith(params INewsCommentQueries.NewsCommentListing[] comments)
    {
        var queries = Substitute.For<INewsCommentQueries>();
        queries.List(default, default!, default, default).ReturnsForAnyArgs(
            new PagedResult<INewsCommentQueries.NewsCommentListing>(
                comments.Length,
                1,
                1,
                10,
                comments.Length > 0 ? Some<IEnumerable<INewsCommentQueries.NewsCommentListing>>(comments) : None));

        return queries;
    }

    private static Task<IResult> ListAs(
        HttpContext context,
        INewsCommentQueries queries,
        params string[] grants) =>
        NewsCommentEndpoints.List(
            NewsId,
            context,
            queries,
            SessionTestHelper.SessionQueries(SessionId, UserId),
            new FakeAuthorizationService(grants),
            Clock);

    private static NewsCommentItem Single(IResult result) =>
        Assert.Single(Assert.IsType<JsonHttpResult<PageResponse<NewsCommentItem>>>(result).Value!.Items);

    [Fact]
    public async Task List_AnonymousViewer_CannotDeleteComments()
    {
        var context = new DefaultHttpContext();

        var item = Single(await ListAs(context, QueriesWith(Comment(UserId))));

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(new NewsCommentItem(CommentId, "Ada", "Nice", Now, false, AuthorId: UserId), item);
    }

    [Fact]
    public async Task List_Author_CanDeleteTheirOwnComment()
    {
        var item = Single(await ListAs(SessionTestHelper.SignedIn(SessionId), QueriesWith(Comment(UserId))));

        Assert.True(item.CanDelete);
    }

    [Fact]
    public async Task List_OtherSignedInUser_CannotDeleteTheComment()
    {
        var item = Single(await ListAs(SessionTestHelper.SignedIn(SessionId), QueriesWith(Comment(OtherUserId))));

        Assert.False(item.CanDelete);
    }

    [Fact]
    public async Task List_Moderator_CanDeleteAnyComment()
    {
        var item = Single(await ListAs(
            SessionTestHelper.SignedIn(SessionId),
            QueriesWith(Comment(OtherUserId)),
            NewsCommentEndpoints.ModerationGrant));

        Assert.True(item.CanDelete);
    }

    [Fact]
    public async Task List_PassesTheNewsAndPaging()
    {
        var queries = QueriesWith();

        await NewsCommentEndpoints.List(
            NewsId,
            new DefaultHttpContext(),
            queries,
            Substitute.For<ISessionQueries>(),
            new FakeAuthorizationService(),
            Clock,
            2,
            1000);

        await queries.Received(1).List(
            NewsId,
            new Flipper(2, NewsCommentEndpoints.MaxPageSize),
            Some(Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_AsksForCommentsOfNewsThatIsLiveAtTheCurrentTime()
    {
        var queries = QueriesWith();

        await ListAs(new DefaultHttpContext(), queries);

        await queries.Received(1).List(NewsId, Arg.Any<Flipper>(), Some(Now), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_StoresTheTrimmedCommentForTheSignedInUser()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Add(default, default, default!, default).ReturnsForAnyArgs(Some(CommentId));

        var result = await NewsCommentEndpoints.Add(
            NewsId,
            new NewsCommentEndpoints.SaveNewsCommentRequest("  Nice  "),
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            commands);

        Assert.Equal(CommentId, Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value!.Id);
        await commands.Received(1).Add(NewsId, UserId, "Nice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_BlankComment_ReturnsBadRequest()
    {
        var commands = Substitute.For<INewsCommentCommands>();

        var result = await NewsCommentEndpoints.Add(
            NewsId,
            new NewsCommentEndpoints.SaveNewsCommentRequest("   "),
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Add(default, default, default!, default);
    }

    [Fact]
    public async Task Add_WithoutValidSession_ReturnsUnauthorized()
    {
        var commands = Substitute.For<INewsCommentCommands>();

        var result = await NewsCommentEndpoints.Add(
            NewsId,
            new NewsCommentEndpoints.SaveNewsCommentRequest("Nice"),
            new DefaultHttpContext(),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            commands);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Add_UnknownNews_ReturnsNotFound()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Add(default, default, default!, default).ReturnsForAnyArgs(None);

        var result = await NewsCommentEndpoints.Add(
            NewsId,
            new NewsCommentEndpoints.SaveNewsCommentRequest("Nice"),
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            commands);

        Assert.IsType<NotFound>(result);
    }

    private static Task<IResult> Delete(
        Guid author,
        INewsCommentCommands commands,
        bool signedIn = true,
        params string[] grants)
    {
        var queries = Substitute.For<INewsCommentQueries>();
        queries.FindAuthor(CommentId, Arg.Any<CancellationToken>()).Returns(Some(author));

        return NewsCommentEndpoints.Delete(
            CommentId,
            signedIn ? SessionTestHelper.SignedIn(SessionId) : new DefaultHttpContext(),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            queries,
            commands,
            new FakeAuthorizationService(grants));
    }

    [Fact]
    public async Task Delete_Author_DeletesTheirOwnComment()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Delete(CommentId, Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContent>(await Delete(UserId, commands));
        await commands.Received(1).Delete(CommentId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Moderator_DeletesSomeoneElsesComment()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Delete(CommentId, Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContent>(await Delete(OtherUserId, commands, true, NewsCommentEndpoints.ModerationGrant));
    }

    [Fact]
    public async Task Delete_OtherUser_IsForbidden()
    {
        var commands = Substitute.For<INewsCommentCommands>();

        var result = await Delete(OtherUserId, commands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await commands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public async Task Delete_WithoutValidSession_ReturnsUnauthorized()
    {
        Assert.IsType<UnauthorizedHttpResult>(await Delete(UserId, Substitute.For<INewsCommentCommands>(), signedIn: false));
    }

    [Fact]
    public async Task Delete_UnknownComment_ReturnsNotFound()
    {
        var queries = Substitute.For<INewsCommentQueries>();
        queries.FindAuthor(default, default).ReturnsForAnyArgs(None);

        var result = await NewsCommentEndpoints.Delete(
            CommentId,
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            queries,
            Substitute.For<INewsCommentCommands>(),
            new FakeAuthorizationService());

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Delete_CommentDeletedMeanwhile_ReturnsNotFound()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Delete(CommentId, Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFound>(await Delete(UserId, commands));
    }

    [Fact]
    public async Task List_AuthorCanEditTheirOwnCommentButNotOthers()
    {
        var own = Single(await ListAs(SessionTestHelper.SignedIn(SessionId), QueriesWith(Comment(UserId))));
        var other = Single(await ListAs(SessionTestHelper.SignedIn(SessionId), QueriesWith(Comment(OtherUserId))));
        var anonymous = Single(await ListAs(new DefaultHttpContext(), QueriesWith(Comment(UserId))));

        Assert.True(own.CanEdit);
        Assert.False(other.CanEdit);
        Assert.False(anonymous.CanEdit);
    }

    [Fact]
    public async Task List_TheEditGrantLetsAModeratorEditAnyCommentButNotDeleteIt()
    {
        var item = Single(await ListAs(
            SessionTestHelper.SignedIn(SessionId),
            QueriesWith(Comment(OtherUserId)),
            NewsCommentEndpoints.EditGrant));

        Assert.True(item.CanEdit);
        Assert.False(item.CanDelete);
    }

    [Fact]
    public async Task List_TheDeleteGrantDoesNotAllowEditing()
    {
        var item = Single(await ListAs(
            SessionTestHelper.SignedIn(SessionId),
            QueriesWith(Comment(OtherUserId)),
            NewsCommentEndpoints.ModerationGrant));

        Assert.True(item.CanDelete);
        Assert.False(item.CanEdit);
    }

    [Fact]
    public async Task List_ReportsWhenTheCommentWasEdited()
    {
        var edited = Comment(UserId) with { UpdatedAt = Some(Now.AddHours(2)) };

        var item = Single(await ListAs(new DefaultHttpContext(), QueriesWith(edited)));

        Assert.Equal(Now.AddHours(2), item.UpdatedAt);
    }

    [Fact]
    public void EditGrant_IsNewsManage()
    {
        Assert.Equal("News.Manage", NewsCommentEndpoints.EditGrant);
    }

    private static Task<IResult> Edit(
        Guid author,
        INewsCommentCommands commands,
        string content = "  Fixed  ",
        bool signedIn = true,
        params string[] grants)
    {
        var queries = Substitute.For<INewsCommentQueries>();
        queries.FindAuthor(CommentId, Arg.Any<CancellationToken>()).Returns(Some(author));

        return NewsCommentEndpoints.Update(
            CommentId,
            new NewsCommentEndpoints.SaveNewsCommentRequest(content),
            signedIn ? SessionTestHelper.SignedIn(SessionId) : new DefaultHttpContext(),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            queries,
            commands,
            new FakeAuthorizationService(grants));
    }

    [Fact]
    public async Task Update_Author_EditsTheirOwnCommentWithTrimmedContent()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Update(CommentId, "Fixed", Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContent>(await Edit(UserId, commands));
        await commands.Received(1).Update(CommentId, "Fixed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ModeratorWithTheEditGrant_EditsSomeoneElsesComment()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Update(CommentId, "Fixed", Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContent>(await Edit(OtherUserId, commands, grants: NewsCommentEndpoints.EditGrant));
    }

    [Fact]
    public async Task Update_OtherUserWithOnlyTheDeleteGrant_IsForbidden()
    {
        var commands = Substitute.For<INewsCommentCommands>();

        var result = await Edit(OtherUserId, commands, grants: NewsCommentEndpoints.ModerationGrant);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await commands.DidNotReceiveWithAnyArgs().Update(default, default!, default);
    }

    [Fact]
    public async Task Update_OtherUser_IsForbidden()
    {
        var commands = Substitute.For<INewsCommentCommands>();

        var result = await Edit(OtherUserId, commands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await commands.DidNotReceiveWithAnyArgs().Update(default, default!, default);
    }

    [Fact]
    public async Task Update_WithoutValidSession_ReturnsUnauthorized()
    {
        Assert.IsType<UnauthorizedHttpResult>(await Edit(UserId, Substitute.For<INewsCommentCommands>(), signedIn: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_BlankContent_ReturnsBadRequest(string content)
    {
        var commands = Substitute.For<INewsCommentCommands>();

        Assert.IsType<BadRequest>(await Edit(UserId, commands, content));
        await commands.DidNotReceiveWithAnyArgs().Update(default, default!, default);
    }

    [Fact]
    public async Task Update_UnknownComment_ReturnsNotFound()
    {
        var queries = Substitute.For<INewsCommentQueries>();
        queries.FindAuthor(default, default).ReturnsForAnyArgs(None);

        var result = await NewsCommentEndpoints.Update(
            CommentId,
            new NewsCommentEndpoints.SaveNewsCommentRequest("Fixed"),
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            queries,
            Substitute.For<INewsCommentCommands>(),
            new FakeAuthorizationService());

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_CommentDeletedMeanwhile_ReturnsNotFound()
    {
        var commands = Substitute.For<INewsCommentCommands>();
        commands.Update(CommentId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        Assert.IsType<NotFound>(await Edit(UserId, commands));
    }
}

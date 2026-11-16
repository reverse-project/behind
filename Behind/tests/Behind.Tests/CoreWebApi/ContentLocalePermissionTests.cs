using System.Security.Claims;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Authorization;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentLocalePermissionTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task NewsCommentUsesTheSameAuthorOrManagePermissionAsItsEditor(bool owner, bool manager, bool expected)
    {
        var viewer = Guid.NewGuid();
        var http = new DefaultHttpContext();
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(http).Returns((Guid?)viewer);
        var comments = Substitute.For<INewsCommentQueries>();
        comments.FindAuthor(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Some(owner ? viewer : Guid.NewGuid()));
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), NewsCommentEndpoints.EditGrant)
            .Returns(manager ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        Assert.Equal(expected, await new NewsCommentLocalePermission(identity, comments, authorization).CanEdit(http, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(true, true, 1, true)]
    [InlineData(false, true, 1, false)]
    [InlineData(true, false, 1, false)]
    [InlineData(true, true, 6, false)]
    public async Task ChatAssignmentRequiresOwnershipWritePermissionAndAnOpenEditWindow(
        bool owner, bool writer, int minutes, bool expected)
    {
        var viewer = Guid.NewGuid();
        var id = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        var http = new DefaultHttpContext();
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(http).Returns((Guid?)viewer);
        var messages = Substitute.For<IChatStore>();
        messages.Find(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new IChatStore.Message(id, 1, owner ? viewer : Guid.NewGuid(), "Text", null, now.AddMinutes(-minutes), null)]);
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(writer ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        Assert.Equal(expected, await new ChatMessageLocalePermission(identity, messages, authorization,
            new FixedTimeProvider(now)).CanEdit(http, id));
    }
}

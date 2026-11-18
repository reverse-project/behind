using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi.Authorization;

public class GrantAuthorizationHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_UsesSessionOwnerInsteadOfReassignedEmail(bool ownerHasGrant)
    {
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var sessions = Substitute.For<ISessionQueries>();
        var access = Substitute.For<IAccessQueries>();
        sessions.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>()).Returns(
            Some<(Session, Guid)>((new ValidSession(AccessToken.Create(new GuestUser(None)),
                RefreshToken.Create(new GuestUser(None)), "ip", "agent"), ownerId)));
        access.GetUserAccess(ownerId, Arg.Any<CancellationToken>()).Returns(
            new UserAccess([], ownerHasGrant ? [(Grant)"AuditLog.Read"] : [], []));
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("session_id", sessionId.ToString()),
            new Claim(ClaimTypes.Email, "reassigned@example.com")], "test"));
        var requirement = new GrantRequirement("AuditLog.Read");
        var context = new AuthorizationHandlerContext([requirement], user, new DefaultHttpContext { User = user });

        await new GrantAuthorizationHandler(sessions, access, NullLogger<GrantAuthorizationHandler>.Instance).HandleAsync(context);

        Assert.Equal(ownerHasGrant, context.HasSucceeded);
        await access.Received(1).GetUserAccess(ownerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmailWithoutSessionCannotAuthorize()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "admin@example.com")], "test"));
        var access = Substitute.For<IAccessQueries>();
        var context = new AuthorizationHandlerContext([new GrantRequirement("AuditLog.Read")], user,
            new DefaultHttpContext { User = user });
        await new GrantAuthorizationHandler(Substitute.For<ISessionQueries>(), access,
            NullLogger<GrantAuthorizationHandler>.Instance).HandleAsync(context);
        Assert.False(context.HasSucceeded);
        await access.DidNotReceiveWithAnyArgs().GetUserAccess(default, default);
    }
}

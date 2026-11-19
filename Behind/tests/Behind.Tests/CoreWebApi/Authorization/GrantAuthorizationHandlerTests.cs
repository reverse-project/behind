using System.Security.Claims;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

        await new GrantAuthorizationHandler(sessions, access, NullLogger<GrantAuthorizationHandler>.Instance,
            new HttpContextAccessor()).HandleAsync(context);

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
            NullLogger<GrantAuthorizationHandler>.Instance, new HttpContextAccessor()).HandleAsync(context);
        Assert.False(context.HasSucceeded);
        await access.DidNotReceiveWithAnyArgs().GetUserAccess(default, default);
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("missing-grant", false)]
    [InlineData("restricted", false)]
    [InlineData("missing-session", false)]
    [InlineData("invalid-session", false)]
    [InlineData("anonymous", false)]
    [InlineData("missing-claim", false)]
    [InlineData("missing-request", false)]
    [InlineData("different-principal", false)]
    public async Task HoldsGrantAsync_UsesTheAuthenticatedRequestSession(string scenario, bool expected)
    {
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var sessions = Substitute.For<ISessionQueries>();
        var access = Substitute.For<IAccessQueries>();
        Option<(Session, Guid)> session = scenario switch
        {
            "missing-session" => None,
            "invalid-session" => Some<(Session, Guid)>((new InvalidSession(None, None), ownerId)),
            _ => Some<(Session, Guid)>((new ValidSession(AccessToken.Create(new GuestUser(None)),
                RefreshToken.Create(new GuestUser(None)), "ip", "agent"), ownerId))
        };
        sessions.TryFindByIdWithUserId(sessionId, cancellation.Token).Returns(session);
        access.GetUserAccess(ownerId, cancellation.Token).Returns(new UserAccess(
            [], scenario == "missing-grant" ? [] : [(Grant)"AuditLog.Read"],
            scenario == "restricted" ? [(Restriction)"AuditLog.Read"] : []));
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            scenario == "missing-claim" ? [] : [new Claim("session_id", sessionId.ToString())],
            scenario == "anonymous" ? null : "test"));
        var request = new DefaultHttpContext { User = user, RequestAborted = cancellation.Token };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(scenario == "missing-request" ? null : request);
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(sessions)
            .AddSingleton(access)
            .AddSingleton(accessor)
            .AddGrantAuthorization()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = scenario == "different-principal"
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", Guid.NewGuid().ToString())], "test"))
            : user;

        Assert.Equal(expected, await authorization.HoldsGrantAsync(principal, "AuditLog.Read"));

        if (scenario is "active" or "missing-grant" or "restricted")
        {
            await access.Received(1).GetUserAccess(ownerId, cancellation.Token);
        }
        else
        {
            await access.DidNotReceiveWithAnyArgs().GetUserAccess(default, default);
        }
    }
}

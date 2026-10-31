using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.WebApi.Auditing;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi.Auditing;

public sealed class HttpAuditActorProviderTests
{
    private readonly IHttpContextAccessor _accessor = Substitute.For<IHttpContextAccessor>();
    private readonly ISessionQueries _sessionQueries = Substitute.For<ISessionQueries>();

    private HttpAuditActorProvider Provider => new(_accessor, _sessionQueries);

    private static DefaultHttpContext ContextWith(params Claim[] claims) =>
        new() { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };

    [Fact]
    public async Task GetCurrentAsync_WithoutHttpContext_IsAutonomous()
    {
        _accessor.HttpContext.Returns((HttpContext?)null);

        Assert.Equal(AuditActor.Autonomous, await Provider.GetCurrentAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_WithoutSessionClaim_IsAnonymous()
    {
        _accessor.HttpContext.Returns(ContextWith());

        Assert.Equal(AuditActor.Anonymous, await Provider.GetCurrentAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_WithKnownSession_IsSessionUser()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _accessor.HttpContext.Returns(ContextWith(new Claim(SessionClaimTypes.SessionId, sessionId.ToString())));
        _sessionQueries.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<(Session Session, Guid UserId)>>(
                Some<(Session Session, Guid UserId)>((new InvalidSession(None, None), userId))));

        Assert.Equal(AuditActor.ForUser(userId), await Provider.GetCurrentAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_WithUnknownSession_IsAnonymous()
    {
        var sessionId = Guid.NewGuid();
        _accessor.HttpContext.Returns(ContextWith(new Claim(SessionClaimTypes.SessionId, sessionId.ToString())));
        _sessionQueries.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<(Session Session, Guid UserId)>>(None));

        Assert.Equal(AuditActor.Anonymous, await Provider.GetCurrentAsync());
    }
}

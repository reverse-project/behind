using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Behind.Common.Database;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ImpersonationEndpointsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly JwtSettings Jwt = new("issuer", "audience", new string('k', 64));
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    private readonly ISessionQueries _sessions = Substitute.For<ISessionQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly ISessionCommands _sessionCommands = Substitute.For<ISessionCommands>();
    private readonly UserManager<ApplicationUser> _userManager = Substitute.For<UserManager<ApplicationUser>>(
        Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
    private readonly UserManagementGate _gate;
    private readonly CapturingLogger<ImpersonationEndpoints> _logger = new();

    public ImpersonationEndpointsTests()
    {
        _gate = new UserManagementGate(
            _sessions, _access, _users, AccessCatalog.Core, new FixedTimeProvider(Now), NullLogger<UserManagementGate>.Instance);
        SignedInAs(Administrator(), impersonated: false);
        TargetIs(Managed(TargetId));
        TargetHolds(BuiltInAccess.Users.Role);
        _userManager.FindByIdAsync(TargetId.ToString()).Returns(new ApplicationUser
        {
            Id = TargetId,
            DisplayName = "Target",
            Emails = [new Email("target@test.com")]
        });
        _sessionCommands.CreateImpersonation(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
    }

    private static Role Administrator() => BuiltInAccess.Administrators.Role;

    private static DefaultHttpContext SignedIn()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("session_id", SessionId.ToString())],
                "test"))
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
        context.Request.Headers.UserAgent = "unit-test";

        return context;
    }

    private static IUserManagementQueries.ManagedUser Managed(
        Guid id,
        string[]? roles = null,
        Option<IWallOfShameQueries.ActiveBan> ban = default) =>
        new(id, "Target", ["target@test.com"], roles ?? [DomainConstants.UsersRole], ban);

    private static IOptions<SecurityConfiguration> Security(Option<JwtSettings> jwt) =>
        Options.Create(new SecurityConfiguration(false, None, jwt, None));

    private static ValidSession SessionOf(bool impersonated) =>
        new(
            AccessToken.Create(new GuestUser(None)),
            RefreshToken.Create(new GuestUser(None)),
            "10.0.0.1",
            "test-agent")
        {
            Impersonator = impersonated
                ? Some<User>(new HumanUser("Admin", [new Email("admin@test.com")], None))
                : None
        };

    private void SignedInAs(Role role, bool impersonated)
    {
        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((SessionOf(impersonated), ActorId)));
        _access.GetUserAccess(ActorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([role], [], []));
    }

    private void TargetIs(IUserManagementQueries.ManagedUser? target) =>
        _users.TryFindUser(TargetId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(target is null ? None : Some(target));

    private void TargetHolds(Role role) =>
        _access.GetUserAccess(TargetId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([role], [], []));

    private Task<IResult> Start(Option<JwtSettings>? jwt = null) =>
        ImpersonationEndpoints.Start(
            TargetId,
            SignedIn(),
            _gate,
            _userManager,
            Security(jwt ?? Some(Jwt)),
            _access,
            _sessionCommands,
            _logger);

    private async Task AssertNoSessionWasCreated() =>
        await _sessionCommands.DidNotReceiveWithAnyArgs().CreateImpersonation(
            default, default, default!, default);

    [Fact]
    public async Task Start_AsAdministrator_CreatesAnImpersonationSessionForTheTarget()
    {
        var sessionId = Guid.NewGuid();
        _sessionCommands.CreateImpersonation(
                ActorId, TargetId, Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(sessionId);

        var result = await Start();

        var response = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result).Value!;
        var created = Assert.IsType<ValidSession>(
            _sessionCommands.ReceivedCalls().Single().GetArguments()[2]);
        Assert.Equal("10.1.2.3", created.RemoteAddress.Value);
        Assert.Equal("unit-test", created.UserAgent.Value);
        Assert.Equal(Now.Add(DomainConstants.ImpersonationDuration), created.RefreshToken.ExpiresAt);
        var access = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal(sessionId.ToString(), access.Claims.Single(c => c.Type == SessionClaimTypes.SessionId).Value);
        Assert.Equal("target@test.com", access.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(created.AccessToken.ExpiresAt, response.Expires);
    }

    [Fact]
    public async Task Start_DisablesCaching()
    {
        var context = SignedIn();

        await ImpersonationEndpoints.Start(
            TargetId, context, _gate, _userManager, Security(Some(Jwt)), _access, _sessionCommands, _logger);

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Start_LogsWhoImpersonatesWhom()
    {
        await Start();

        Assert.Contains(
            _logger.Entries,
            entry => entry.Level == LogLevel.Information
                     && entry.Message.Contains(ActorId.ToString())
                     && entry.Message.Contains(TargetId.ToString()));
    }

    [Fact]
    public async Task Start_WithoutJwtSettings_ReturnsInternalServerError()
    {
        var result = await Start(None);

        Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_WithoutSession_ReturnsUnauthorized()
    {
        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<(Session, Guid)>>(None));

        Assert.IsType<UnauthorizedHttpResult>(await Start());
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_UnknownTarget_ReturnsNotFound()
    {
        TargetIs(null);

        Assert.IsType<NotFound>(await Start());
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_TargetNoLongerInTheIdentityStore_ReturnsNotFound()
    {
        _userManager.FindByIdAsync(TargetId.ToString()).Returns((ApplicationUser?)null);

        Assert.IsType<NotFound>(await Start());
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_ActorWithoutTheImpersonationGrant_IsForbidden()
    {
        SignedInAs(BuiltInAccess.Users.Role, impersonated: false);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await Start()).StatusCode);
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_WhileAlreadyImpersonating_IsForbidden()
    {
        SignedInAs(Administrator(), impersonated: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await Start()).StatusCode);
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_OnTheActorThemself_IsForbidden()
    {
        TargetIs(Managed(ActorId));
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(Managed(ActorId)));

        var result = await ImpersonationEndpoints.Start(
            ActorId, SignedIn(), _gate, _userManager, Security(Some(Jwt)), _access, _sessionCommands, _logger);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_OnABannedTarget_IsForbidden()
    {
        TargetIs(Managed(TargetId, ban: Some(new IWallOfShameQueries.ActiveBan("rude", None))));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await Start()).StatusCode);
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_OnADeveloper_AsAnAdministrator_IsForbidden()
    {
        TargetIs(Managed(TargetId, [DomainConstants.DevelopersRole]));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await Start()).StatusCode);
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Start_OnATargetHoldingGrantsTheActorLacks_IsForbidden()
    {
        TargetHolds(new UserRole("Custom", Some<IEnumerable<Grant>>([new Grant("Configuration.Read")])));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(await Start()).StatusCode);
        await AssertNoSessionWasCreated();
    }

    [Fact]
    public async Task Stop_OnAnImpersonationSession_InvalidatesItAsTheImpersonator()
    {
        var impersonatorId = Guid.NewGuid();
        SignedInAs(Administrator(), impersonated: true);
        _sessions.TryFindActor(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some(new ISessionQueries.SessionActor(ActorId, Some(impersonatorId))));

        var result = await ImpersonationEndpoints.Stop(
            SignedIn(), _sessions, _sessionCommands, new FixedTimeProvider(Now), _logger);

        Assert.IsType<NoContent>(result);
        await _sessionCommands.Received(1).InvalidateSession(
            SessionId, impersonatorId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_OnAPlainSession_ReturnsConflict()
    {
        _sessions.TryFindActor(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some(new ISessionQueries.SessionActor(ActorId, None)));

        var result = await ImpersonationEndpoints.Stop(
            SignedIn(), _sessions, _sessionCommands, new FixedTimeProvider(Now), _logger);

        Assert.IsType<Conflict>(result);
        await _sessionCommands.DidNotReceiveWithAnyArgs().InvalidateSession(default, default, default, default);
    }

    [Fact]
    public async Task Stop_WithoutSession_ReturnsUnauthorized()
    {
        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<(Session, Guid)>>(None));

        var result = await ImpersonationEndpoints.Stop(
            SignedIn(), _sessions, _sessionCommands, new FixedTimeProvider(Now), _logger);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task UserList_OffersImpersonationOnlyWhereItIsAllowed()
    {
        var plain = Managed(TargetId);
        var banned = Managed(Guid.NewGuid(), ban: Some(new IWallOfShameQueries.ActiveBan("rude", None)));
        var self = Managed(ActorId);
        _users.ListUsers(
                new Flipper(1, UserManagementEndpoints.DefaultPageSize), None, None, DeletionState.Active, Now,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(
                3, 1, 1, UserManagementEndpoints.DefaultPageSize,
                Some<IEnumerable<IUserManagementQueries.ManagedUser>>([plain, banned, self])));
        _users.GetLetters(None, DeletionState.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["T"]));

        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now));

        var items = Assert.IsType<JsonHttpResult<UserListResponse>>(result).Value!.Users.Items;
        Assert.Equal([true, false, false], items.Select(item => item.Permissions.CanImpersonate));
    }

    [Fact]
    public async Task UserList_WhileImpersonating_OffersNoImpersonation()
    {
        SignedInAs(Administrator(), impersonated: true);
        _users.ListUsers(
                new Flipper(1, UserManagementEndpoints.DefaultPageSize), None, None, DeletionState.Active, Now,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(
                1, 1, 1, UserManagementEndpoints.DefaultPageSize,
                Some<IEnumerable<IUserManagementQueries.ManagedUser>>([Managed(TargetId)])));
        _users.GetLetters(None, DeletionState.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["T"]));

        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now));

        var item = Assert.Single(Assert.IsType<JsonHttpResult<UserListResponse>>(result).Value!.Users.Items);
        Assert.False(item.Permissions.CanImpersonate);
    }

    [Fact]
    public void SessionResponse_OfAnImpersonationSession_NamesTheImpersonator()
    {
        var session = SessionOf(impersonated: true);
        var user = new HumanUser("Target", [new Email("target@test.com")], None);

        var response = (session with { User = user }).ToSessionResponse(
            SessionId, TargetId, user, UserAccess.Empty, Now, null);

        var impersonator = Assert.IsType<SessionImpersonator>(response.Impersonator);
        Assert.Equal("Admin", impersonator.DisplayName);
        Assert.Equal(["admin@test.com"], impersonator.Emails);
    }

    [Fact]
    public void SessionResponse_OfAPlainSession_HasNoImpersonator()
    {
        var user = new HumanUser("Target", [new Email("target@test.com")], None);

        var response = SessionOf(impersonated: false).ToSessionResponse(
            SessionId, TargetId, user, UserAccess.Empty, Now, null);

        Assert.Null(response.Impersonator);
    }
}

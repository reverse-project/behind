using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class UserManagementEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    private readonly ISessionQueries _sessions = Substitute.For<ISessionQueries>();
    private readonly IRoleQueries _roles = Substitute.For<IRoleQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly IRoleCommands _roleCommands = Substitute.For<IRoleCommands>();
    private readonly ISessionCommands _sessionCommands = Substitute.For<ISessionCommands>();
    private readonly IWallOfShameCommands _wallOfShame = Substitute.For<IWallOfShameCommands>();
    private readonly UserManagementGate _gate;

    public UserManagementEndpointsTests()
    {
        _gate = new UserManagementGate(_sessions, _roles, _users, new FixedTimeProvider(Now));
        SignedInAs(ActorId, developer: false);
    }

    private static DefaultHttpContext SignedIn() =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("session_id", SessionId.ToString())],
                "test"))
        };

    private static IUserManagementQueries.ManagedUser Managed(
        Guid id,
        string displayName = "Target",
        string[]? roles = null,
        Option<IWallOfShameQueries.ActiveBan> ban = default) =>
        new(id, displayName, [$"{displayName.ToLowerInvariant()}@test.com"], roles ?? [DomainConstants.UsersRole], ban);

    private void SignedInAs(Guid userId, bool developer)
    {
        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((
                new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent"),
                userId)));

        _roles.GetRolesByUserId(userId, Arg.Any<CancellationToken>())
            .Returns(developer
                ? [(Role)BuiltInAccess.Developers.Role with { Grants = Some<IEnumerable<Grant>>(BuiltInAccess.Developers.Grants.Select(g => g.Grant).ToList()) }]
                : [(Role)BuiltInAccess.Administrators.Role]);
    }

    private void TargetIs(IUserManagementQueries.ManagedUser? target) =>
        _users.TryFindUser(TargetId, Now, Arg.Any<CancellationToken>())
            .Returns(target is null ? None : Some(target));

    [Fact]
    public async Task Get_ReturnsUsersWithPermissionsForTheActor()
    {
        var self = Managed(ActorId, "Self", [DomainConstants.AdministratorsRole]);
        var banned = Managed(
            TargetId,
            "Banned",
            ban: Some(new IWallOfShameQueries.ActiveBan("rude", None)));
        var developer = Managed(Guid.NewGuid(), "Dev", [DomainConstants.DevelopersRole]);
        _users.ListUsers(new Flipper(1, UserManagementEndpoints.DefaultPageSize), None, Now, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(
                3,
                1,
                1,
                UserManagementEndpoints.DefaultPageSize,
                Some<IEnumerable<IUserManagementQueries.ManagedUser>>([self, banned, developer])));

        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now));

        var page = Assert.IsType<JsonHttpResult<Behind.Common.WebApi.PageResponse<ManagedUserItem>>>(result).Value!;
        Assert.Equal(3, page.Total);
        var items = page.Items;
        Assert.True(items[0].IsCurrent);
        Assert.True(items[0].IsAdministrator);
        Assert.False(items[0].Permissions.CanModerate);
        Assert.Equal(new BanInfo("rude", null), items[1].Ban);
        Assert.Equal(new ManagedUserPermissions(true, false, true), items[1].Permissions);
        Assert.True(items[2].IsDeveloper);
        Assert.Equal(new ManagedUserPermissions(false, false, false), items[2].Permissions);
    }

    [Fact]
    public async Task Get_DisablesCachingAndPassesSearchAndPaging()
    {
        var context = SignedIn();
        _users.ListUsers(
                new Flipper(3, UserManagementEndpoints.MaxPageSize),
                Some("ada"),
                Now,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(0, 3, 0, 100, None));

        var result = await UserManagementEndpoints.Get(
            context,
            _gate,
            _users,
            new FixedTimeProvider(Now),
            page: 3,
            pageSize: 100000,
            search: "  ada ");

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        var body = Assert.IsType<JsonHttpResult<Behind.Common.WebApi.PageResponse<ManagedUserItem>>>(result).Value!;
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task Get_WithoutSession_ReturnsUnauthorized()
    {
        var result = await UserManagementEndpoints.Get(
            new DefaultHttpContext { User = new ClaimsPrincipal() },
            _gate,
            _users,
            new FixedTimeProvider(Now));

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Elevate_Administrator_AssignsTheRole()
    {
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.Elevate(TargetId, "administrator", SignedIn(), _gate, _roleCommands);

        Assert.IsType<NoContent>(result);
        await _roleCommands.Received(1).AssignBuiltIn(TargetId, BuiltInAccess.Administrators, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Elevate_Developer_AsAdministrator_IsForbidden()
    {
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.Elevate(TargetId, "developer", SignedIn(), _gate, _roleCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _roleCommands.DidNotReceiveWithAnyArgs().AssignBuiltIn(default, default!, default);
    }

    [Fact]
    public async Task Elevate_Developer_AsDeveloper_AssignsTheRole()
    {
        SignedInAs(ActorId, developer: true);
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.Elevate(TargetId, "developer", SignedIn(), _gate, _roleCommands);

        Assert.IsType<NoContent>(result);
        await _roleCommands.Received(1).AssignBuiltIn(TargetId, BuiltInAccess.Developers, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Elevate_UnknownRole_ReturnsNotFound()
    {
        var result = await UserManagementEndpoints.Elevate(TargetId, "users", SignedIn(), _gate, _roleCommands);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Elevate_UnknownUser_ReturnsNotFound()
    {
        TargetIs(null);

        var result = await UserManagementEndpoints.Elevate(TargetId, "administrator", SignedIn(), _gate, _roleCommands);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Elevate_Yourself_IsForbidden()
    {
        _users.TryFindUser(ActorId, Now, Arg.Any<CancellationToken>())
            .Returns(Some(Managed(ActorId, "Self")));

        var result = await UserManagementEndpoints.Elevate(ActorId, "administrator", SignedIn(), _gate, _roleCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Elevate_WithoutSession_ReturnsUnauthorized()
    {
        var result = await UserManagementEndpoints.Elevate(
            TargetId,
            "administrator",
            new DefaultHttpContext { User = new ClaimsPrincipal() },
            _gate,
            _roleCommands);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Deelevate_Administrator_RevokesTheRole()
    {
        TargetIs(Managed(TargetId, roles: [DomainConstants.AdministratorsRole]));

        var result = await UserManagementEndpoints.Deelevate(TargetId, "administrator", SignedIn(), _gate, _roleCommands);

        Assert.IsType<NoContent>(result);
        await _roleCommands.Received(1).RevokeBuiltIn(TargetId, BuiltInAccess.Administrators, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deelevate_DeveloperTarget_AsAdministrator_IsForbidden()
    {
        TargetIs(Managed(TargetId, roles: [DomainConstants.DevelopersRole, DomainConstants.AdministratorsRole]));

        var result = await UserManagementEndpoints.Deelevate(TargetId, "administrator", SignedIn(), _gate, _roleCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _roleCommands.DidNotReceiveWithAnyArgs().RevokeBuiltIn(default, default!, default);
    }

    [Fact]
    public async Task SignOut_InvalidatesEverySessionOfTheTarget()
    {
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.SignOut(TargetId, SignedIn(), _gate, _sessionCommands);

        Assert.IsType<NoContent>(result);
        await _sessionCommands.Received(1).InvalidateAllSessions(TargetId, ActorId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignOut_Yourself_IsForbidden()
    {
        _users.TryFindUser(ActorId, Now, Arg.Any<CancellationToken>()).Returns(Some(Managed(ActorId)));

        var result = await UserManagementEndpoints.SignOut(ActorId, SignedIn(), _gate, _sessionCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _sessionCommands.DidNotReceiveWithAnyArgs().InvalidateAllSessions(default, default, default, default);
    }

    [Fact]
    public async Task Ban_StoresTheTrimmedReasonAndSignsTheUserOutEverywhere()
    {
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.Ban(
            TargetId,
            new UserManagementEndpoints.BanRequest("  spamming  "),
            SignedIn(),
            _gate,
            _wallOfShame,
            _sessionCommands);

        Assert.IsType<NoContent>(result);
        await _wallOfShame.Received(1).Ban(TargetId, ActorId, new Reason("spamming"), Arg.Any<CancellationToken>());
        await _sessionCommands.Received(1).InvalidateAllSessions(TargetId, ActorId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ban_DeveloperTarget_AsAdministrator_IsForbidden()
    {
        TargetIs(Managed(TargetId, roles: [DomainConstants.DevelopersRole]));

        var result = await UserManagementEndpoints.Ban(
            TargetId,
            new UserManagementEndpoints.BanRequest("reason"),
            SignedIn(),
            _gate,
            _wallOfShame,
            _sessionCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _wallOfShame.DidNotReceiveWithAnyArgs().Ban(default, default, default!, default);
        await _sessionCommands.DidNotReceiveWithAnyArgs().InvalidateAllSessions(default, default, default, default);
    }

    [Fact]
    public async Task Ban_DeveloperTarget_AsDeveloper_IsAllowed()
    {
        SignedInAs(ActorId, developer: true);
        TargetIs(Managed(TargetId, roles: [DomainConstants.DevelopersRole]));

        var result = await UserManagementEndpoints.Ban(
            TargetId,
            new UserManagementEndpoints.BanRequest("reason"),
            SignedIn(),
            _gate,
            _wallOfShame,
            _sessionCommands);

        Assert.IsType<NoContent>(result);
    }

    [Theory]
    [InlineData("spamming", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void BanRequest_RequiresANonBlankReason(string reason, bool valid)
    {
        var request = new UserManagementEndpoints.BanRequest(reason);

        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public void BanRequest_RejectsAReasonLongerThanTheLimit()
    {
        var request = new UserManagementEndpoints.BanRequest(new string('x', UserManagementEndpoints.MaxReasonLength + 1));

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public async Task Unban_RemovesTheBan()
    {
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.Unban(TargetId, SignedIn(), _gate, _wallOfShame);

        Assert.IsType<NoContent>(result);
        await _wallOfShame.Received(1).Unban(TargetId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unban_UnknownUser_ReturnsNotFound()
    {
        TargetIs(null);

        var result = await UserManagementEndpoints.Unban(TargetId, SignedIn(), _gate, _wallOfShame);

        Assert.IsType<NotFound>(result);
        await _wallOfShame.DidNotReceiveWithAnyArgs().Unban(default, default);
    }
}

using System.Security.Claims;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class UserAccessEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();
    private static readonly Guid SupportRoleId = Guid.NewGuid();

    private readonly ISessionQueries _sessions = Substitute.For<ISessionQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly IRoleQueries _roles = Substitute.For<IRoleQueries>();
    private readonly IAccessCommands _accessCommands = Substitute.For<IAccessCommands>();
    private readonly UserManagementGate _gate;

    public UserAccessEndpointsTests()
    {
        _gate = new UserManagementGate(_sessions, _access, _users, new FixedTimeProvider(Now));
        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((
                new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent"),
                ActorId)));
        _access.GetUserAccess(ActorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(BuiltInAccess.Administrators)], [], []));
        _access.GetUserAccess(TargetId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(BuiltInAccess.Users)], [], []));
        _users.TryFindUser(TargetId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(
                TargetId,
                "Target",
                ["target@test.com"],
                [DomainConstants.UsersRole],
                None)));
        _roles.ListRoles(Arg.Any<CancellationToken>()).Returns(
        [
            new IRoleQueries.RoleResult(BuiltInAccess.Users.Id, Granted(BuiltInAccess.Users)),
            new IRoleQueries.RoleResult(BuiltInAccess.Administrators.Id, Granted(BuiltInAccess.Administrators)),
            new IRoleQueries.RoleResult(BuiltInAccess.Developers.Id, Granted(BuiltInAccess.Developers)),
            new IRoleQueries.RoleResult(SupportRoleId, new UserRole("Support", Some<IEnumerable<Grant>>([new Grant("Role.Manage")])))
        ]);
    }

    private static Role Granted(BuiltInRole role) =>
        role.Role with { Grants = Some<IEnumerable<Grant>>(role.Grants.Select(grant => grant.Grant).ToList()) };

    private static DefaultHttpContext SignedIn() =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", SessionId.ToString())], "test"))
        };

    private Task<IResult> Set(
        Guid[] roleIds,
        string[] grants,
        string[] restrictions,
        Guid? target = null) =>
        UserAccessEndpoints.Set(
            target ?? TargetId,
            new UserAccessEndpoints.UserAccessRequest(roleIds, grants, restrictions),
            SignedIn(),
            _gate,
            _access,
            _roles,
            _accessCommands);

    [Fact]
    public async Task Get_ReturnsRolesDirectAccessAndCalculatedGrants()
    {
        _access.GetUserAccess(TargetId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(BuiltInAccess.Users)], ["Role.Manage"], ["Session.Read"]));
        var context = SignedIn();

        var result = await UserAccessEndpoints.Get(TargetId, context, _gate, _access, _roles);

        var body = Assert.IsType<JsonHttpResult<UserAccessResponse>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(TargetId, body.UserId);
        Assert.True(body.CanEdit);
        Assert.Equal([DomainConstants.UsersRole], body.Roles.Select(role => role.Name));
        Assert.Equal(4, body.AvailableRoles.Count);
        Assert.True(body.AvailableRoles.Single(role => role.Id == BuiltInAccess.Users.Id).IsRequired);
        Assert.Equal(["Role.Manage"], body.Grants);
        Assert.Equal(["Session.Read"], body.Restrictions);
        Assert.Contains("Role.Manage", body.CalculatedGrants);
        Assert.DoesNotContain("Session.Read", body.CalculatedGrants);
        Assert.Equal(BuiltInAccess.GrantNames, body.AvailableGrants);
    }

    [Fact]
    public async Task Get_Yourself_IsReadOnly()
    {
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(ActorId, "Me", ["me@test.com"], [], None)));

        var result = await UserAccessEndpoints.Get(ActorId, SignedIn(), _gate, _access, _roles);

        Assert.False(Assert.IsType<JsonHttpResult<UserAccessResponse>>(result).Value!.CanEdit);
    }

    [Fact]
    public async Task Get_UnknownUser_ReturnsNotFound()
    {
        var result = await UserAccessEndpoints.Get(Guid.NewGuid(), SignedIn(), _gate, _access, _roles);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Set_RolesAndRestrictions_AreSaved()
    {
        var result = await Set(
            [BuiltInAccess.Users.Id, SupportRoleId],
            [],
            ["Session.Read", "Session.Read"]);

        Assert.IsType<NoContent>(result);
        await _accessCommands.Received(1).SetUserAccess(
            TargetId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2),
            Arg.Is<AccessSelection>(selection =>
                selection.Grants.Count == 0 && selection.Restrictions.SequenceEqual(new[] { "Session.Read" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Set_WithoutTheUsersRole_ReturnsBadRequest()
    {
        var result = await Set([SupportRoleId], [], []);

        Assert.IsType<BadRequest>(result);
        await _accessCommands.DidNotReceiveWithAnyArgs().SetUserAccess(default, default!, default!, default);
    }

    [Fact]
    public async Task Set_UnknownRole_ReturnsBadRequest()
    {
        var result = await Set([BuiltInAccess.Users.Id, Guid.NewGuid()], [], []);

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Set_InvalidSelection_ReturnsBadRequest()
    {
        var result = await Set([BuiltInAccess.Users.Id], ["Nope.Read"], []);

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Set_AssigningARoleWithGrantsTheActorLacks_IsForbidden()
    {
        var result = await Set([BuiltInAccess.Users.Id, BuiltInAccess.Developers.Id], [], []);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _accessCommands.DidNotReceiveWithAnyArgs().SetUserAccess(default, default!, default!, default);
    }

    [Fact]
    public async Task Set_GrantingADirectGrantTheActorLacks_IsForbidden()
    {
        var result = await Set([BuiltInAccess.Users.Id], ["RawDatabase.Manage"], []);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Set_AsDeveloper_MayAssignTheDeveloperRole()
    {
        _access.GetUserAccess(ActorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(BuiltInAccess.Developers)], [], []));

        var result = await Set([BuiltInAccess.Users.Id, BuiltInAccess.Developers.Id], [], []);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Set_LiftingARestrictionTheActorLacks_IsForbidden()
    {
        _access.GetUserAccess(TargetId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(BuiltInAccess.Users)], ["RawDatabase.Manage"], ["RawDatabase.Manage"]));

        var result = await Set([BuiltInAccess.Users.Id], ["RawDatabase.Manage"], []);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Set_RemovingGrants_IsAllowedEvenWhenTheActorLacksThem()
    {
        _access.GetUserAccess(TargetId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(BuiltInAccess.Users)], ["RawDatabase.Manage"], []));

        var result = await Set([BuiltInAccess.Users.Id], [], []);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Set_Yourself_IsForbidden()
    {
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(ActorId, "Me", ["me@test.com"], [], None)));

        var result = await Set([BuiltInAccess.Users.Id], [], [], ActorId);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Set_UnknownUser_ReturnsNotFound()
    {
        var result = await Set([BuiltInAccess.Users.Id], [], [], Guid.NewGuid());

        Assert.IsType<NotFound>(result);
    }
}

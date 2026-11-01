using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class RoleManagementEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid CustomRoleId = Guid.NewGuid();

    private readonly ISessionQueries _sessions = Substitute.For<ISessionQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly IRoleQueries _roles = Substitute.For<IRoleQueries>();
    private readonly IRoleCommands _roleCommands = Substitute.For<IRoleCommands>();
    private readonly IAccessCommands _accessCommands = Substitute.For<IAccessCommands>();
    private readonly UserManagementGate _gate;

    public RoleManagementEndpointsTests()
    {
        _gate = new UserManagementGate(
            _sessions, _access, _users, new FixedTimeProvider(Now), NullLogger<UserManagementGate>.Instance);
        SignedInAs(ActorRoles(BuiltInAccess.Administrators));
    }

    private static DefaultHttpContext SignedIn() =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", SessionId.ToString())], "test"))
        };

    private static Role ActorRoles(BuiltInRole role) =>
        role.Role with { Grants = Some<IEnumerable<Grant>>(role.Grants.Select(grant => grant.Grant).ToList()) };

    private static Role Custom(string[] grants, string[]? restrictions = null) =>
        new UserRole("Support", Some<IEnumerable<Grant>>(grants.Select(grant => new Grant(grant)).ToList()))
        {
            Restrictions = Some<IEnumerable<Restriction>>(
                (restrictions ?? []).Select(restriction => new Restriction(restriction)).ToList())
        };

    private void SignedInAs(Role role)
    {
        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((
                new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent"),
                ActorId)));

        _access.GetUserAccess(ActorId, Arg.Any<CancellationToken>()).Returns(new UserAccess([role], [], []));
    }

    private void RoleExists(Guid id, Role role) =>
        _roles.GetRoleById(id, Arg.Any<CancellationToken>()).Returns(Some(new IRoleQueries.RoleResult(id, role)));

    private static DefaultHttpContext Anonymous() => new() { User = new ClaimsPrincipal() };

    private static PagedResult<IRoleQueries.RoleResult> Page(params IRoleQueries.RoleResult[] roles) =>
        new(roles.Length, 1, 1, 10, roles.Length > 0 ? Some<IEnumerable<IRoleQueries.RoleResult>>(roles) : None);

    private void RolesPage(DeletionState state, params IRoleQueries.RoleResult[] roles)
    {
        _roles.PageRoles(Arg.Any<Flipper>(), Arg.Any<Option<string>>(), Arg.Any<Option<string>>(), state, Arg.Any<CancellationToken>())
            .Returns(Page(roles));
        _roles.GetLetters(Arg.Any<Option<string>>(), state, Arg.Any<CancellationToken>()).Returns(["A", "S"]);
    }

    [Fact]
    public async Task Get_ListsRolesWithWhatTheActorMayDo()
    {
        RolesPage(
            DeletionState.Active,
            new IRoleQueries.RoleResult(BuiltInAccess.Administrators.Id, ActorRoles(BuiltInAccess.Administrators)),
            new IRoleQueries.RoleResult(BuiltInAccess.Developers.Id, ActorRoles(BuiltInAccess.Developers)),
            new IRoleQueries.RoleResult(CustomRoleId, Custom(["User.Read"], ["User.Read"])));
        var context = SignedIn();

        var result = await RoleManagementEndpoints.Get(context, _gate, _roles, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<RoleListResponse>>(result).Value!;
        var roles = body.Roles.Items;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(BuiltInAccess.GrantNames, body.AvailableGrants);
        Assert.Equal(["A", "S"], body.Letters);
        Assert.True(roles[0].IsBuiltIn);
        Assert.True(roles[0].CanEdit);
        Assert.False(roles[0].CanDelete);
        Assert.False(roles[1].CanEdit);
        Assert.False(roles[2].IsBuiltIn);
        Assert.True(roles[2].CanDelete);
        Assert.False(roles[2].IsDeleted);
        Assert.Equal(["User.Read"], roles[2].Restrictions);
    }

    [Fact]
    public async Task Get_PassesTheSearchLetterAndPaging()
    {
        RolesPage(DeletionState.Active);

        await RoleManagementEndpoints.Get(SignedIn(), _gate, _roles, new FakeAuthorizationService(), 2, 500, " sup ", "s");

        await _roles.Received(1).PageRoles(
            new Flipper(2, RoleManagementEndpoints.MaxPageSize),
            Some("sup"),
            Some("S"),
            DeletionState.Active,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_UnknownLetter_ReturnsBadRequest()
    {
        var result = await RoleManagementEndpoints.Get(SignedIn(), _gate, _roles, new FakeAuthorizationService(), letter: "xx");

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Get_DeletedRoles_NeedTheDeletedRecordsGrant()
    {
        var result = await RoleManagementEndpoints.Get(SignedIn(), _gate, _roles, new FakeAuthorizationService(), deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Get_DeletedRoles_AreMarkedAndCannotBeEditedOrDeleted()
    {
        RolesPage(DeletionState.Deleted, new IRoleQueries.RoleResult(CustomRoleId, Custom(["User.Read"], [])));

        var result = await RoleManagementEndpoints.Get(
            SignedIn(),
            _gate,
            _roles,
            new FakeAuthorizationService(RoleManagementEndpoints.DeletedRecordsGrant),
            deleted: true);

        var role = Assert.Single(Assert.IsType<JsonHttpResult<RoleListResponse>>(result).Value!.Roles.Items);
        Assert.True(role.IsDeleted);
        Assert.False(role.CanEdit);
        Assert.False(role.CanDelete);
    }

    [Fact]
    public async Task Get_WithoutSession_ReturnsUnauthorized()
    {
        var result = await RoleManagementEndpoints.Get(Anonymous(), _gate, _roles, new FakeAuthorizationService());

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Restore_DeletedRole_ReturnsNoContent()
    {
        _roleCommands.Restore(CustomRoleId, Arg.Any<CancellationToken>()).Returns(true);

        Assert.IsType<NoContent>(await RoleManagementEndpoints.Restore(CustomRoleId, SignedIn(), _roleCommands));
    }

    [Fact]
    public async Task Restore_RoleThatIsNotDeleted_ReturnsNotFound()
    {
        Assert.IsType<NotFound>(await RoleManagementEndpoints.Restore(CustomRoleId, SignedIn(), _roleCommands));
    }

    [Fact]
    public async Task HardDelete_MapsTheOutcome()
    {
        _roleCommands.HardDelete(CustomRoleId, Arg.Any<CancellationToken>()).Returns(
            HardDeleteResult.Deleted,
            HardDeleteResult.NotFound,
            HardDeleteResult.Rejected);

        Assert.IsType<NoContent>(await RoleManagementEndpoints.HardDelete(CustomRoleId, SignedIn(), _roleCommands));
        Assert.IsType<NotFound>(await RoleManagementEndpoints.HardDelete(CustomRoleId, SignedIn(), _roleCommands));
        Assert.IsType<Conflict>(await RoleManagementEndpoints.HardDelete(CustomRoleId, SignedIn(), _roleCommands));
    }

    [Fact]
    public async Task Create_NewName_CreatesTheTrimmedRole()
    {
        _roleCommands.Create("Support", Arg.Any<CancellationToken>()).Returns(Some(Guid.NewGuid()));

        var result = await RoleManagementEndpoints.Create(
            new RoleManagementEndpoints.CreateRoleRequest("  Support "),
            SignedIn(),
            _gate,
            _roleCommands);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task Create_ExistingName_ReturnsConflict()
    {
        _roleCommands.Create("Support", Arg.Any<CancellationToken>()).Returns(None);

        var result = await RoleManagementEndpoints.Create(
            new RoleManagementEndpoints.CreateRoleRequest("Support"),
            SignedIn(),
            _gate,
            _roleCommands);

        Assert.IsType<Conflict>(result);
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        var result = await RoleManagementEndpoints.Create(
            new RoleManagementEndpoints.CreateRoleRequest("   "),
            SignedIn(),
            _gate,
            _roleCommands);

        Assert.IsType<BadRequest>(result);
        await _roleCommands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task Create_WithoutSession_ReturnsUnauthorized()
    {
        var result = await RoleManagementEndpoints.Create(
            new RoleManagementEndpoints.CreateRoleRequest("Support"),
            Anonymous(),
            _gate,
            _roleCommands);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SetAccess_ValidSelection_SavesTheNormalizedSelection()
    {
        RoleExists(CustomRoleId, Custom([]));

        var result = await RoleManagementEndpoints.SetAccess(
            CustomRoleId,
            new RoleManagementEndpoints.AccessRequest(["User.Read", "User.Read"], ["Session.Read"]),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.IsType<NoContent>(result);
        await _accessCommands.Received(1).SetRoleAccess(
            CustomRoleId,
            Arg.Is<AccessSelection>(selection =>
                selection.Grants.SequenceEqual(new[] { "User.Read" })
                && selection.Restrictions.SequenceEqual(new[] { "Session.Read" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAccess_UnknownGrant_ReturnsBadRequest()
    {
        RoleExists(CustomRoleId, Custom([]));

        var result = await RoleManagementEndpoints.SetAccess(
            CustomRoleId,
            new RoleManagementEndpoints.AccessRequest(["Nope.Read"], []),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task SetAccess_GrantAndRestrictionOverlap_ReturnsBadRequest()
    {
        RoleExists(CustomRoleId, Custom([]));

        var result = await RoleManagementEndpoints.SetAccess(
            CustomRoleId,
            new RoleManagementEndpoints.AccessRequest(["User.Read"], ["User.Read"]),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task SetAccess_AdministratorGrantingWhatTheyLack_IsForbidden()
    {
        RoleExists(CustomRoleId, Custom([]));

        var result = await RoleManagementEndpoints.SetAccess(
            CustomRoleId,
            new RoleManagementEndpoints.AccessRequest(["RawDatabase.Manage"], []),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _accessCommands.DidNotReceiveWithAnyArgs().SetRoleAccess(default, default!, default);
    }

    [Fact]
    public async Task SetAccess_DeveloperGrantingAnything_Saves()
    {
        SignedInAs(ActorRoles(BuiltInAccess.Developers));
        RoleExists(CustomRoleId, Custom([]));

        var result = await RoleManagementEndpoints.SetAccess(
            CustomRoleId,
            new RoleManagementEndpoints.AccessRequest(["RawDatabase.Manage"], []),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task SetAccess_RoleHoldingGrantsTheActorLacks_IsForbidden()
    {
        RoleExists(BuiltInAccess.Developers.Id, ActorRoles(BuiltInAccess.Developers));

        var result = await RoleManagementEndpoints.SetAccess(
            BuiltInAccess.Developers.Id,
            new RoleManagementEndpoints.AccessRequest([], ["Session.Read"]),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task SetAccess_LiftingARestrictionTheActorLacks_IsForbidden()
    {
        RoleExists(CustomRoleId, Custom(["RawDatabase.Manage"], ["RawDatabase.Manage"]));

        var result = await RoleManagementEndpoints.SetAccess(
            CustomRoleId,
            new RoleManagementEndpoints.AccessRequest(["RawDatabase.Manage"], []),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task SetAccess_UnknownRole_ReturnsNotFound()
    {
        _roles.GetRoleById(CustomRoleId, Arg.Any<CancellationToken>()).Returns(None);

        var result = await RoleManagementEndpoints.SetAccess(
            CustomRoleId,
            new RoleManagementEndpoints.AccessRequest([], []),
            SignedIn(),
            _gate,
            _roles,
            _accessCommands);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Delete_CustomRole_DeletesIt()
    {
        RoleExists(CustomRoleId, Custom(["User.Read"]));

        var result = await RoleManagementEndpoints.Delete(CustomRoleId, SignedIn(), _gate, _roles, _roleCommands);

        Assert.IsType<NoContent>(result);
        await _roleCommands.Received(1).Delete(CustomRoleId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_BuiltInRole_IsForbidden()
    {
        RoleExists(BuiltInAccess.Users.Id, ActorRoles(BuiltInAccess.Users));

        var result = await RoleManagementEndpoints.Delete(BuiltInAccess.Users.Id, SignedIn(), _gate, _roles, _roleCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _roleCommands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public async Task Delete_UnknownRole_ReturnsNotFound()
    {
        _roles.GetRoleById(CustomRoleId, Arg.Any<CancellationToken>()).Returns(None);

        var result = await RoleManagementEndpoints.Delete(CustomRoleId, SignedIn(), _gate, _roles, _roleCommands);

        Assert.IsType<NotFound>(result);
    }
}

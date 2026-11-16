using System.Security.Claims;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ModuleAccessEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    private static readonly AccessCatalog WithNotes =
        AccessCatalog.From(NotesModule.Catalog).GetOrElse(AccessCatalog.Core);

    private readonly ISessionQueries _sessions = Substitute.For<ISessionQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly IRoleQueries _roles = Substitute.For<IRoleQueries>();
    private readonly IAccessCommands _accessCommands = Substitute.For<IAccessCommands>();

    public ModuleAccessEndpointsTests()
    {
        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((
                new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent"),
                ActorId)));
        _access.GetUserAccess(ActorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(WithNotes, BuiltInAccess.Administrators.Id)], [], []));
        _access.GetUserAccess(TargetId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([Granted(WithNotes, BuiltInAccess.Users.Id)], [], []));
        _users.TryFindUser(TargetId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(
                TargetId,
                "Target",
                ["target@test.com"],
                [DomainConstants.UsersRole],
                None)));
        _roles.ListRoles(Arg.Any<CancellationToken>()).Returns(
        [
            new IRoleQueries.RoleResult(BuiltInAccess.Users.Id, Granted(WithNotes, BuiltInAccess.Users.Id)),
            new IRoleQueries.RoleResult(BuiltInAccess.Administrators.Id, Granted(WithNotes, BuiltInAccess.Administrators.Id))
        ]);
    }

    private static Role Granted(AccessCatalog catalog, Guid roleId) =>
        catalog.FindRole(roleId).Match(
            () => throw new InvalidOperationException(),
            role => role.Role with { Grants = Some<IEnumerable<Grant>>(role.Grants.Select(grant => grant.Grant).ToList()) });

    private UserManagementGate Gate(AccessCatalog catalog) =>
        new(_sessions, _access, _users, catalog, new FixedTimeProvider(Now), NullLogger<UserManagementGate>.Instance);

    private static DefaultHttpContext SignedIn() =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", SessionId.ToString())], "test"))
        };

    [Fact]
    public async Task UserAccess_OffersTheGrantsOfTheModules()
    {
        var result = await UserAccessEndpoints.Get(TargetId, SignedIn(), Gate(WithNotes), _access, _roles);

        var body = Assert.IsType<JsonHttpResult<UserAccessResponse>>(result).Value!;
        Assert.Equal(WithNotes.GrantNames, body.AvailableGrants);
        Assert.Contains("Notes.Write", body.AvailableGrants);
        Assert.Contains("Notes.Read", body.CalculatedGrants);
    }

    [Fact]
    public async Task UserAccess_WithoutModulesDoesNotOfferModuleGrants()
    {
        var result = await UserAccessEndpoints.Get(TargetId, SignedIn(), Gate(AccessCatalog.Core), _access, _roles);

        var body = Assert.IsType<JsonHttpResult<UserAccessResponse>>(result).Value!;
        Assert.DoesNotContain("Notes.Write", body.AvailableGrants);
    }

    [Fact]
    public async Task UserAccess_AcceptsADirectModuleGrant()
    {
        var result = await UserAccessEndpoints.Set(
            TargetId,
            new UserAccessEndpoints.UserAccessRequest([BuiltInAccess.Users.Id], ["Notes.Write"], []),
            SignedIn(),
            Gate(WithNotes),
            _access,
            _roles,
            _accessCommands);

        Assert.IsType<NoContent>(result);
        await _accessCommands.Received().SetUserAccess(
            TargetId,
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Is<AccessSelection>(selection => selection.Grants.SequenceEqual(new[] { "Notes.Write" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserAccess_RejectsAModuleGrantTheCatalogDoesNotKnow()
    {
        var result = await UserAccessEndpoints.Set(
            TargetId,
            new UserAccessEndpoints.UserAccessRequest([BuiltInAccess.Users.Id], ["Notes.Write"], []),
            SignedIn(),
            Gate(AccessCatalog.Core),
            _access,
            _roles,
            _accessCommands);

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Roles_OfferTheGrantsOfTheModules()
    {
        _roles.PageRoles(Arg.Any<Flipper>(), Arg.Any<Option<string>>(), Arg.Any<Option<string>>(), DeletionState.Active, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IRoleQueries.RoleResult>(0, 1, 1, 10, None));
        _roles.GetLetters(Arg.Any<Option<string>>(), DeletionState.Active, Arg.Any<CancellationToken>()).Returns([]);

        var result = await RoleManagementEndpoints.Get(SignedIn(), Gate(WithNotes), _roles, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<RoleListResponse>>(result).Value!;
        Assert.Equal(WithNotes.GrantNames, body.AvailableGrants);
    }

    [Fact]
    public void Actor_CannotGrantModuleDeveloperGrantsWithoutBeingADeveloper()
    {
        var internals = new ModuleDescriptor("internals", "Internals")
        {
            GrantAreas = [new ModuleGrantArea("Internals", ModuleAudience.Developer)]
        };
        var catalog = AccessCatalog.From(ModuleCatalog.Create([internals]).GetOrElse(ModuleCatalog.Empty))
            .GetOrElse(AccessCatalog.Core);
        var access = new UserAccess([], ["Internals.Read"], []);
        var actor = new UserManagementActor(ActorId, false, false, access, catalog);
        var developer = actor with { IsDeveloper = true };

        Assert.False(actor.CanGrant([new Grant("Internals.Read")]));
        Assert.True(developer.CanGrant([new Grant("Internals.Read")]));
    }
}

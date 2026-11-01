using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Behind.Common.Database;
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
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly IRoleCommands _roleCommands = Substitute.For<IRoleCommands>();
    private readonly ISessionCommands _sessionCommands = Substitute.For<ISessionCommands>();
    private readonly IWallOfShameCommands _wallOfShame = Substitute.For<IWallOfShameCommands>();
    private readonly IUserCommands _userCommands = Substitute.For<IUserCommands>();
    private readonly UserManagementGate _gate;
    private readonly CapturingLogger<UserManagementGate> _gateLogger = new();

    public UserManagementEndpointsTests()
    {
        _gate = new UserManagementGate(
            _sessions, _access, _users, new FixedTimeProvider(Now), _gateLogger);
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

        _access.GetUserAccess(userId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess(
                developer
                    ? [(Role)BuiltInAccess.Developers.Role with { Grants = Some<IEnumerable<Grant>>(BuiltInAccess.Developers.Grants.Select(g => g.Grant).ToList()) }]
                    : [(Role)BuiltInAccess.Administrators.Role],
                [],
                []));
    }

    private void DeletedTargetIs(IUserManagementQueries.ManagedUser? target) =>
        _users.TryFindUser(TargetId, DeletionState.Deleted, Now, Arg.Any<CancellationToken>())
            .Returns(target is null ? None : Some(target));

    private void TargetIs(IUserManagementQueries.ManagedUser? target) =>
        _users.TryFindUser(TargetId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
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
        _users.ListUsers(new Flipper(1, UserManagementEndpoints.DefaultPageSize), None, None, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(
                3,
                1,
                1,
                UserManagementEndpoints.DefaultPageSize,
                Some<IEnumerable<IUserManagementQueries.ManagedUser>>([self, banned, developer])));
        _users.GetLetters(None, DeletionState.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["B", "D", "S"]));

        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now));

        var response = Assert.IsType<JsonHttpResult<UserListResponse>>(result).Value!;
        Assert.Equal(["B", "D", "S"], response.Letters);
        var page = response.Users;
        Assert.Equal(3, page.Total);
        var items = page.Items;
        Assert.True(items[0].IsCurrent);
        Assert.True(items[0].IsAdministrator);
        Assert.False(items[0].Permissions.CanModerate);
        Assert.Equal(new BanInfo("rude", null), items[1].Ban);
        Assert.Equal(new ManagedUserPermissions(true, false, true, true, false), items[1].Permissions);
        Assert.True(items[2].IsDeveloper);
        Assert.Equal(new ManagedUserPermissions(false, false, false, false, false), items[2].Permissions);
    }

    [Fact]
    public async Task Get_DisablesCachingAndPassesSearchLetterAndPaging()
    {
        var context = SignedIn();
        _users.ListUsers(
                new Flipper(3, UserManagementEndpoints.MaxPageSize),
                Some("ada"),
                Some("A"),
                DeletionState.Active,
                Now,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(0, 3, 0, 100, None));
        _users.GetLetters(Some("ada"), DeletionState.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["A"]));

        var result = await UserManagementEndpoints.Get(
            context,
            _gate,
            _users,
            new FixedTimeProvider(Now),
            page: 3,
            pageSize: 100000,
            search: "  ada ",
            letter: " a ");

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        var body = Assert.IsType<JsonHttpResult<UserListResponse>>(result).Value!;
        Assert.Empty(body.Users.Items);
        Assert.Equal(["A"], body.Letters);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("1")]
    [InlineData("%")]
    public async Task Get_InvalidLetter_ReturnsBadRequest(string letter)
    {
        var result = await UserManagementEndpoints.Get(
            SignedIn(),
            _gate,
            _users,
            new FixedTimeProvider(Now),
            letter: letter);

        Assert.IsType<BadRequest>(result);
        await _users.DidNotReceiveWithAnyArgs().ListUsers(default!, default, default, default, default);
    }

    [Fact]
    public async Task Get_OtherLetterBucket_IsAccepted()
    {
        _users.ListUsers(
                Arg.Any<Flipper>(),
                None,
                Some("#"),
                DeletionState.Active,
                Now,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(0, 1, 0, 10, None));
        _users.GetLetters(None, DeletionState.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([]));

        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now), letter: "#");

        Assert.IsType<JsonHttpResult<UserListResponse>>(result);
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
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
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
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>()).Returns(Some(Managed(ActorId)));

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
    public async Task SignOut_Allowed_LogsInformationWithActorTargetAndStatus()
    {
        TargetIs(Managed(TargetId));

        await UserManagementEndpoints.SignOut(TargetId, SignedIn(), _gate, _sessionCommands);

        var entry = _gateLogger.Single(LogLevel.Information);
        Assert.Equal(ActorId, entry.State["ActorId"]);
        Assert.Equal(TargetId, entry.State["TargetId"]);
        Assert.Equal(StatusCodes.Status204NoContent, entry.State["StatusCode"]);
    }

    [Fact]
    public async Task SignOut_Forbidden_LogsWarningsWithActorAndTarget()
    {
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>()).Returns(Some(Managed(ActorId)));

        await UserManagementEndpoints.SignOut(ActorId, SignedIn(), _gate, _sessionCommands);

        Assert.All(
            _gateLogger.Entries.Where(entry => entry.Level == LogLevel.Warning),
            entry => Assert.Equal(ActorId, entry.State["ActorId"]));
        Assert.False(_gateLogger.Has(LogLevel.Information));
        Assert.Single(_gateLogger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("not allowed"));
    }

    [Fact]
    public async Task SignOut_UnknownTarget_LogsWarningWithTarget()
    {
        TargetIs(null);

        await UserManagementEndpoints.SignOut(TargetId, SignedIn(), _gate, _sessionCommands);

        var entry = _gateLogger.Single(LogLevel.Warning);
        Assert.Equal(TargetId, entry.State["TargetId"]);
        Assert.Equal(DeletionState.Active, entry.State["State"]);
    }

    [Fact]
    public async Task SignOut_WithoutSession_LogsWarning()
    {
        await UserManagementEndpoints.SignOut(TargetId, new DefaultHttpContext(), _gate, _sessionCommands);

        Assert.Equal(LogLevel.Warning, Assert.Single(_gateLogger.Entries).Level);
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

    [Fact]
    public async Task Get_Deleted_AsAdministrator_IsForbidden()
    {
        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now), deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _users.DidNotReceiveWithAnyArgs().ListUsers(default!, default, default, default, default, default);
    }

    [Fact]
    public async Task Get_Deleted_AsDeveloper_ListsDeletedUsersWithRestorePermissions()
    {
        SignedInAs(ActorId, developer: true);
        var removed = Managed(TargetId, "Removed") with { State = DeletionState.Deleted };
        _users.ListUsers(new Flipper(1, UserManagementEndpoints.DefaultPageSize), None, None, DeletionState.Deleted, Now, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(
                1,
                1,
                1,
                UserManagementEndpoints.DefaultPageSize,
                Some<IEnumerable<IUserManagementQueries.ManagedUser>>([removed])));
        _users.GetLetters(None, DeletionState.Deleted, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(["R"]));

        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now), deleted: true);

        var item = Assert.Single(Assert.IsType<JsonHttpResult<UserListResponse>>(result).Value!.Users.Items);
        Assert.True(item.IsDeleted);
        Assert.Equal(new ManagedUserPermissions(false, false, false, false, true), item.Permissions);
    }

    [Fact]
    public async Task Delete_AsAdministrator_DeletesTheTarget()
    {
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.Delete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.IsType<NoContent>(result);
        await _userCommands.Received(1).Delete(TargetId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_UnknownUser_ReturnsNotFound()
    {
        TargetIs(null);

        var result = await UserManagementEndpoints.Delete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.IsType<NotFound>(result);
        await _userCommands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public async Task Delete_Yourself_IsForbidden()
    {
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>()).Returns(Some(Managed(ActorId)));

        var result = await UserManagementEndpoints.Delete(ActorId, SignedIn(), _gate, _userCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _userCommands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public async Task Delete_DeveloperTarget_AsAdministrator_IsForbidden()
    {
        TargetIs(Managed(TargetId, roles: [DomainConstants.DevelopersRole]));

        var result = await UserManagementEndpoints.Delete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _userCommands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public async Task Delete_WithoutTheDeleteGrant_IsForbidden()
    {
        SignedInAs(ActorId, developer: true);
        TargetIs(Managed(TargetId));

        var result = await UserManagementEndpoints.Delete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _userCommands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public async Task Restore_AsDeveloper_RestoresTheDeletedTarget()
    {
        SignedInAs(ActorId, developer: true);
        DeletedTargetIs(Managed(TargetId) with { State = DeletionState.Deleted });

        var result = await UserManagementEndpoints.Restore(TargetId, SignedIn(), _gate, _userCommands);

        Assert.IsType<NoContent>(result);
        await _userCommands.Received(1).Restore(TargetId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_AsAdministrator_IsForbidden()
    {
        DeletedTargetIs(Managed(TargetId) with { State = DeletionState.Deleted });

        var result = await UserManagementEndpoints.Restore(TargetId, SignedIn(), _gate, _userCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _userCommands.DidNotReceiveWithAnyArgs().Restore(default, default);
    }

    [Fact]
    public async Task Restore_UserThatIsNotDeleted_ReturnsNotFound()
    {
        SignedInAs(ActorId, developer: true);
        DeletedTargetIs(null);

        var result = await UserManagementEndpoints.Restore(TargetId, SignedIn(), _gate, _userCommands);

        Assert.IsType<NotFound>(result);
        await _userCommands.DidNotReceiveWithAnyArgs().Restore(default, default);
    }

    [Fact]
    public async Task HardDelete_AsDeveloper_RemovesTheDeletedTarget()
    {
        SignedInAs(ActorId, developer: true);
        DeletedTargetIs(Managed(TargetId) with { State = DeletionState.Deleted });
        _userCommands.HardDelete(TargetId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await UserManagementEndpoints.HardDelete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task HardDelete_RejectedByTheDatabase_ReturnsConflict()
    {
        SignedInAs(ActorId, developer: true);
        DeletedTargetIs(Managed(TargetId) with { State = DeletionState.Deleted });
        _userCommands.HardDelete(TargetId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await UserManagementEndpoints.HardDelete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.IsType<Conflict>(result);
    }

    [Fact]
    public async Task HardDelete_AsAdministrator_IsForbidden()
    {
        DeletedTargetIs(Managed(TargetId) with { State = DeletionState.Deleted });

        var result = await UserManagementEndpoints.HardDelete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await _userCommands.DidNotReceiveWithAnyArgs().HardDelete(default, default);
    }

    [Fact]
    public async Task HardDelete_UserThatIsNotDeleted_ReturnsNotFound()
    {
        SignedInAs(ActorId, developer: true);
        DeletedTargetIs(null);

        var result = await UserManagementEndpoints.HardDelete(TargetId, SignedIn(), _gate, _userCommands);

        Assert.IsType<NotFound>(result);
        await _userCommands.DidNotReceiveWithAnyArgs().HardDelete(default, default);
    }

    [Fact]
    public async Task ModerationActions_OnADeletedUser_AreNotOffered()
    {
        SignedInAs(ActorId, developer: true);
        var removed = Managed(TargetId) with { State = DeletionState.Deleted };
        _users.ListUsers(Arg.Any<Flipper>(), None, None, DeletionState.Deleted, Now, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IUserManagementQueries.ManagedUser>(
                1, 1, 1, 10, Some<IEnumerable<IUserManagementQueries.ManagedUser>>([removed])));
        _users.GetLetters(None, DeletionState.Deleted, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>([]));

        var result = await UserManagementEndpoints.Get(SignedIn(), _gate, _users, new FixedTimeProvider(Now), deleted: true);

        var permissions = Assert.Single(Assert.IsType<JsonHttpResult<UserListResponse>>(result).Value!.Users.Items).Permissions;
        Assert.False(permissions.CanModerate);
        Assert.False(permissions.CanChangeAdministrator);
    }
}

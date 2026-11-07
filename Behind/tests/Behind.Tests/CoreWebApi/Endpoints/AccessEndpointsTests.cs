using System.Security.Claims;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class AccessEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();
    private static readonly VersionedKey<Guid> SettingsId = new(AccessSettings.SingletonKey, 2);

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly IAccessSettingsQueries _settings = Substitute.For<IAccessSettingsQueries>();
    private readonly IAccessSettingsCommands _commands = Substitute.For<IAccessSettingsCommands>();
    private readonly IRoleQueries _roles = Substitute.For<IRoleQueries>();
    private readonly IUserApprovalQueries _approvalQueries = Substitute.For<IUserApprovalQueries>();
    private readonly IUserApprovalCommands _approvalCommands = Substitute.For<IUserApprovalCommands>();
    private readonly IUserCommands _userCommands = Substitute.For<IUserCommands>();
    private readonly ISessionQueries _sessions = Substitute.For<ISessionQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly UserManagementGate _gate;

    public AccessEndpointsTests()
    {
        _gate = new UserManagementGate(
            _sessions, _access, _users, new FixedTimeProvider(Now), NullLogger<UserManagementGate>.Instance);

        _sessions.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((
                new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent"),
                ActorId)));
        _access.GetUserAccess(ActorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([(Role)BuiltInAccess.Administrators.Role], [], []));
    }

    private static DefaultHttpContext SignedIn() =>
        new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", SessionId.ToString())], "test")) };

    private static AccessEndpoints.SaveAccessSettingsRequest Request(
        string role = " Users ",
        string? message = "  Members only  ") =>
        new(true, role, message, true);

    private void TargetIs(IUserManagementQueries.ManagedUser? target) =>
        _users.TryFindUser(TargetId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(target is null ? None : Some(target));

    private static IUserManagementQueries.ManagedUser Managed(Guid id, params string[] roles) =>
        new(id, "Target", ["target@test.com"], roles.Length > 0 ? roles : [DomainConstants.UsersRole], None);

    private static void AssertStatus(IResult result, int status)
    {
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Restriction_ReportsTheDecisionAndIsNotCached()
    {
        _settings.Current(Arg.Any<CancellationToken>()).Returns(new AccessSettings(true, "Users", "Members only", false));
        var gate = new AccessGate(_settings, _sessions, _access);
        var httpContext = new DefaultHttpContext();

        var result = await AccessEndpoints.Restriction(httpContext, gate);

        Assert.Equal(
            new RestrictionStatusResponse(true, "Members only"),
            Assert.IsType<JsonHttpResult<RestrictionStatusResponse>>(result).Value);
        Assert.Equal(ManagedListExtensions.NoStore, httpContext.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task UploadAccess_StoresTheImageUnderTheAccessKindAndReturnsItsOwnUrl()
    {
        var id = Guid.NewGuid();
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(id);
        IFormFile file = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "logo.png");

        var result = await SiteImageEndpoints.UploadAccess(
            file,
            new DefaultHttpContext(),
            commands,
            FixedOptionsSnapshot.Of(new SitesConfiguration()));

        Assert.Equal(
            new SiteImageResponse(id, $"/access/images/{id}"),
            Assert.IsType<JsonHttpResult<SiteImageResponse>>(result).Value);
        await commands.Received(1).Add(
            Arg.Is<IVfsCommands.Upload>(upload => upload.Type == VfsTypes.AccessSettings && upload.ContentType == "image/png"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAccess_UsesTheConfiguredImageLimit()
    {
        IFormFile file = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "logo.png");

        var result = await SiteImageEndpoints.UploadAccess(
            file,
            new DefaultHttpContext(),
            Substitute.For<IVfsCommands>(),
            FixedOptionsSnapshot.Of(new SitesConfiguration { MaxImageBytes = 4 }));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<JsonHttpResult<SiteImageLimitResponse>>(result).StatusCode);
    }

    [Fact]
    public async Task GetAccess_ServesOnlyAccessImages()
    {
        var id = Guid.NewGuid();
        var queries = Substitute.For<IVfsQueries>();
        queries.Open(VfsTypes.AccessImages, id, Arg.Any<CancellationToken>())
            .Returns(Some(new IVfsQueries.Content(new VfsFile(VfsTypes.AccessSettings, "a.png", "image/png", 3), Png)));
        queries.Open(Arg.Is<IReadOnlyList<string>>(types => !types.SequenceEqual(VfsTypes.AccessImages)), id, Arg.Any<CancellationToken>())
            .Returns(None);

        Assert.IsType<FileContentHttpResult>(await SiteImageEndpoints.GetAccess(id, new DefaultHttpContext(), queries));
        Assert.IsType<NotFound>(await SiteImageEndpoints.GetNews(id, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task Restriction_ReportsTheTitleOfTheRestrictionPage()
    {
        _settings.Current(Arg.Any<CancellationToken>())
            .Returns(new AccessSettings(true, "Users", "<p>Hi</p>", false, "Private club"));
        var gate = new AccessGate(_settings, _sessions, _access);

        var result = await AccessEndpoints.Restriction(new DefaultHttpContext(), gate);

        Assert.Equal(
            new RestrictionStatusResponse(true, "<p>Hi</p>", "Private club"),
            Assert.IsType<JsonHttpResult<RestrictionStatusResponse>>(result).Value);
    }

    [Fact]
    public async Task Restriction_OpenWebsite_HidesTheMessage()
    {
        _settings.Current(Arg.Any<CancellationToken>()).Returns(AccessSettings.Open with { RestrictionMessage = "Hidden" });
        var gate = new AccessGate(_settings, _sessions, _access);

        var result = await AccessEndpoints.Restriction(new DefaultHttpContext(), gate);

        Assert.Equal(
            new RestrictionStatusResponse(false, string.Empty),
            Assert.IsType<JsonHttpResult<RestrictionStatusResponse>>(result).Value);
    }

    [Fact]
    public async Task Manage_ReturnsTheCurrentSettingsAndTheSortedRoleNames()
    {
        _settings.Current(Arg.Any<CancellationToken>()).Returns(new AccessSettings(true, "Users", "Members only", true));
        _roles.ListRoles(Arg.Any<CancellationToken>()).Returns(
        [
            new IRoleQueries.RoleResult(Guid.NewGuid(), new UserRole("Users")),
            new IRoleQueries.RoleResult(Guid.NewGuid(), new UserRole("Administrators"))
        ]);

        var result = await AccessEndpoints.Manage(new DefaultHttpContext(), _settings, _roles, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<AccessManageResponse>>(result).Value!;
        Assert.Equal(new AccessSettingsResponse(true, "Users", "Members only", true, false), body.Settings);
        Assert.Equal(["Administrators", "Users"], body.Roles);
    }

    [Fact]
    public async Task Manage_DeletedSettings_NeedTheDeletedRecordsGrant()
    {
        var result = await AccessEndpoints.Manage(
            new DefaultHttpContext(), _settings, _roles, new FakeAuthorizationService(), deleted: true);

        AssertStatus(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Manage_DeletedSettings_AreMarkedDeleted()
    {
        _settings.FindDeleted(Arg.Any<CancellationToken>())
            .Returns(Some(new AccessSettings(true, "Users", "Old", false)));
        _roles.ListRoles(Arg.Any<CancellationToken>()).Returns([]);

        var result = await AccessEndpoints.Manage(
            new DefaultHttpContext(),
            _settings,
            _roles,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.IsType<JsonHttpResult<AccessManageResponse>>(result).Value!.Settings.IsDeleted);
    }

    [Fact]
    public async Task Manage_NoDeletedSettings_IsNotFound()
    {
        _settings.FindDeleted(Arg.Any<CancellationToken>()).Returns(None);
        _roles.ListRoles(Arg.Any<CancellationToken>()).Returns([]);

        var result = await AccessEndpoints.Manage(
            new DefaultHttpContext(),
            _settings,
            _roles,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Save_TrimsTheFieldsAndAnswersTheSavedVersion()
    {
        _commands.Save(default!, default).ReturnsForAnyArgs(new AccessSettingsSaveResult.Saved(SettingsId));

        var result = await AccessEndpoints.Save(Request(), new DefaultHttpContext(), _commands);

        Assert.Equal(
            new NewsSavedResponse(AccessSettings.SingletonKey, 2),
            Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await _commands.Received(1).Save(
            new IAccessSettingsCommands.AccessSettingsDraft(true, "Users", "Members only", true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_SanitizesTheMessageAndTrimsTheTitle()
    {
        _commands.Save(default!, default).ReturnsForAnyArgs(new AccessSettingsSaveResult.Saved(SettingsId));
        var request = new AccessEndpoints.SaveAccessSettingsRequest(
            true,
            "Users",
            "<p>Members <strong>only</strong></p><script>alert(1)</script>",
            false,
            "  Private club  ");

        await AccessEndpoints.Save(request, new DefaultHttpContext(), _commands);

        await _commands.Received(1).Save(
            new IAccessSettingsCommands.AccessSettingsDraft(
                true,
                "Users",
                "<p>Members <strong>only</strong></p>",
                false,
                "Private club"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_MissingMessage_IsStoredAsEmpty()
    {
        _commands.Save(default!, default).ReturnsForAnyArgs(new AccessSettingsSaveResult.Saved(SettingsId));

        await AccessEndpoints.Save(Request(message: null), new DefaultHttpContext(), _commands);

        await _commands.Received(1).Save(
            new IAccessSettingsCommands.AccessSettingsDraft(true, "Users", string.Empty, true),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(typeof(AccessSettingsSaveResult.UnknownRole), "unknown-role")]
    [InlineData(typeof(AccessSettingsSaveResult.VersionConflict), "version-conflict")]
    public async Task Save_ConflictingOutcomes_AnswerTheirReason(Type outcome, string reason)
    {
        _commands.Save(default!, default).ReturnsForAnyArgs((AccessSettingsSaveResult)Activator.CreateInstance(outcome)!);

        var result = await AccessEndpoints.Save(Request(), new DefaultHttpContext(), _commands);

        Assert.Equal(reason, Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Delete_MapsTheOutcomeOfTheCommand(bool removed, int status)
    {
        _commands.Delete(Arg.Any<CancellationToken>()).Returns(removed);

        AssertStatus(await AccessEndpoints.Delete(new DefaultHttpContext(), _commands), status);
    }

    [Fact]
    public async Task Restore_Saved_IsNoContent()
    {
        _commands.Restore(Arg.Any<CancellationToken>()).Returns(new AccessSettingsSaveResult.Saved(SettingsId));

        AssertStatus(await AccessEndpoints.Restore(new DefaultHttpContext(), _commands), 204);
    }

    [Fact]
    public async Task Restore_NothingDeleted_IsNotFound()
    {
        _commands.Restore(Arg.Any<CancellationToken>()).Returns(new AccessSettingsSaveResult.NotFound());

        AssertStatus(await AccessEndpoints.Restore(new DefaultHttpContext(), _commands), 404);
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, 204)]
    [InlineData(HardDeleteResult.NotFound, 404)]
    [InlineData(HardDeleteResult.Rejected, 409)]
    public async Task HardDelete_MapsEveryOutcomeOfTheCommand(HardDeleteResult outcome, int status)
    {
        _commands.HardDelete(Arg.Any<CancellationToken>()).Returns(outcome);

        AssertStatus(await AccessEndpoints.HardDelete(new DefaultHttpContext(), _commands), status);
    }

    [Fact]
    public async Task PendingUsers_ListsThePendingUsersWithPaging()
    {
        _approvalQueries.ListPending(default!, default, default).ReturnsForAnyArgs(
            new PagedResult<IUserApprovalQueries.PendingUser>(
                1,
                1,
                1,
                10,
                Some<IEnumerable<IUserApprovalQueries.PendingUser>>(
                    [new IUserApprovalQueries.PendingUser(TargetId, "Ada", ["ada@test.com"])])));

        var result = await AccessEndpoints.PendingUsers(new DefaultHttpContext(), _approvalQueries, 2, 5, " ada ");

        var body = Assert.IsType<JsonHttpResult<PendingUsersResponse>>(result).Value!;
        var user = Assert.Single(body.Users.Items);
        Assert.Equal((TargetId, "Ada"), (user.Id, user.DisplayName));
        Assert.Equal(["ada@test.com"], user.Emails);
        await _approvalQueries.Received(1).ListPending(new Flipper(2, 5), Some("ada"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_PendingUser_IsApproved()
    {
        TargetIs(Managed(TargetId));
        _approvalCommands.Approve(TargetId, Arg.Any<CancellationToken>()).Returns(true);

        AssertStatus(await AccessEndpoints.Approve(TargetId, SignedIn(), _gate, _approvalCommands), 204);
    }

    [Fact]
    public async Task Approve_UserThatIsNotPending_IsNotFound()
    {
        TargetIs(Managed(TargetId));
        _approvalCommands.Approve(TargetId, Arg.Any<CancellationToken>()).Returns(false);

        AssertStatus(await AccessEndpoints.Approve(TargetId, SignedIn(), _gate, _approvalCommands), 404);
    }

    [Fact]
    public async Task Approve_UnknownUser_IsNotFound()
    {
        TargetIs(null);

        AssertStatus(await AccessEndpoints.Approve(TargetId, SignedIn(), _gate, _approvalCommands), 404);
        await _approvalCommands.DidNotReceiveWithAnyArgs().Approve(default, default);
    }

    [Fact]
    public async Task Approve_Yourself_IsForbidden()
    {
        _users.TryFindUser(ActorId, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(Managed(ActorId)));

        AssertStatus(await AccessEndpoints.Approve(ActorId, SignedIn(), _gate, _approvalCommands), 403);
        await _approvalCommands.DidNotReceiveWithAnyArgs().Approve(default, default);
    }

    [Fact]
    public async Task Reject_PendingUser_DeletesTheAccount()
    {
        TargetIs(Managed(TargetId));
        _approvalQueries.IsPending(TargetId, Arg.Any<CancellationToken>()).Returns(true);

        AssertStatus(await AccessEndpoints.Reject(TargetId, SignedIn(), _gate, _approvalQueries, _userCommands), 204);
        await _userCommands.Received(1).Delete(TargetId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_UserThatIsNotPending_IsNotFoundAndNothingIsDeleted()
    {
        TargetIs(Managed(TargetId));
        _approvalQueries.IsPending(TargetId, Arg.Any<CancellationToken>()).Returns(false);

        AssertStatus(await AccessEndpoints.Reject(TargetId, SignedIn(), _gate, _approvalQueries, _userCommands), 404);
        await _userCommands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public async Task Reject_ActorWithoutTheUserDeleteGrant_IsForbidden()
    {
        _access.GetUserAccess(ActorId, Arg.Any<CancellationToken>()).Returns(UserAccess.Empty);
        TargetIs(Managed(TargetId));
        _approvalQueries.IsPending(TargetId, Arg.Any<CancellationToken>()).Returns(true);

        AssertStatus(await AccessEndpoints.Reject(TargetId, SignedIn(), _gate, _approvalQueries, _userCommands), 403);
        await _userCommands.DidNotReceiveWithAnyArgs().Delete(default, default);
    }

    [Fact]
    public void SaveRequest_RequiresARoleAndLimitsTheMessageLength()
    {
        Assert.True(IsValid(new AccessEndpoints.SaveAccessSettingsRequest(true, "Users", "Hello", false)));
        Assert.True(IsValid(new AccessEndpoints.SaveAccessSettingsRequest(false, "Users", null, false)));
        Assert.False(IsValid(new AccessEndpoints.SaveAccessSettingsRequest(true, string.Empty, "Hello", false)));
        Assert.False(IsValid(new AccessEndpoints.SaveAccessSettingsRequest(
            true,
            "Users",
            new string('a', AccessSettings.MaxMessageLength + 1),
            false)));
        Assert.False(IsValid(new AccessEndpoints.SaveAccessSettingsRequest(
            true,
            new string('a', AccessSettings.MaxRoleLength + 1),
            "Hello",
            false)));
    }

    private static bool IsValid(object request) =>
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request,
            new System.ComponentModel.DataAnnotations.ValidationContext(request),
            [],
            true);
}

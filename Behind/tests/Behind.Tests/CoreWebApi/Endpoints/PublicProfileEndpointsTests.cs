using System.Security.Claims;
using System.Text.Json;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class PublicProfileEndpointsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ISessionQueries _sessions = Substitute.For<ISessionQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();
    private readonly IUserManagementQueries _users = Substitute.For<IUserManagementQueries>();
    private readonly Guid _sessionId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    public void Dispose() => _factory.Dispose();

    private UserProfiles Profiles(AbstractCoreContext context) => new(context, Substitute.For<IModuleFiles>());

    private Task<IResult> GetProfile(Guid id, HttpContext http, AbstractCoreContext context) =>
        PublicProfileEndpoints.Get(id, http, Profiles(context), Gate(), _users, _access,
            new EmailQueries(context), new TwoFactorQueries(context), Clock);

    private UserManagementGate Gate() =>
        new(_sessions, _access, _users, AccessCatalog.Core, Clock, NullLogger<UserManagementGate>.Instance);

    private HttpContext SignedIn(Guid id, bool impersonated = false)
    {
        _sessions.TryFindByIdWithUserId(_sessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((new ValidSession(
                AccessToken.Create(new GuestUser(None)), RefreshToken.Create(new GuestUser(None)), "ip", "agent")
            {
                Impersonator = impersonated ? Some<User>(new GuestUser(None)) : None
            }, id)));
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", _sessionId.ToString())], "test"))
        };
    }

    [Theory]
    [InlineData(null, "user#uvg8s6o9")]
    [InlineData("  ", "user#uvg8s6o9")]
    [InlineData(" Alex ", "Alex#uvg8s6o9")]
    public void Labels_HashTheEntireNormalizedUuid(string? name, string expected)
    {
        var id = Guid.Parse("019a7821-1234-7000-8000-012345678901");
        Assert.Equal(expected, id.ToUserLabel(name));
        Assert.NotEqual(id.ToUserLabel(name), Guid.Parse("019a7821-1234-7000-8000-012345678902").ToUserLabel(name));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "user#aajyzuzp")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff", "user#93trmt2d")]
    public void Labels_UseEightLowercaseAlphanumericCharacters(string id, string expected)
    {
        Assert.Equal(expected, Guid.Parse(id).ToUserLabel(null));
    }

    [Fact]
    public async Task PrivateMessageActionRequiresBothGrantsAndAnotherHumanProfile()
    {
        await using var context = _factory.CreateContext();
        var viewer = await DatabaseSeedHelper.SeedHumanUserAsync(context, "viewer@test.com");
        var target = await DatabaseSeedHelper.SeedHumanUserAsync(context, "target@test.com");
        foreach (var grants in new[]
        {
            Array.Empty<Grant>(),
            new[] { new Grant("PrivateMessage.Read") },
            new[] { new Grant("PrivateMessage.Write") },
            new[] { new Grant("PrivateMessage.Read"), new Grant("PrivateMessage.Write") }
        })
        {
            _access.GetUserAccess(viewer, Arg.Any<CancellationToken>()).Returns(new UserAccess([], grants, []));
            var response = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
                await GetProfile(target, SignedIn(viewer), context));
            Assert.Equal(grants.Length == 2, response.Value!.Permissions.CanSendPrivateMessage);
        }
        var own = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(viewer, SignedIn(viewer), context));
        Assert.False(own.Value!.Permissions.CanSendPrivateMessage);
        var anonymous = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(target, new DefaultHttpContext(), context));
        Assert.False(anonymous.Value!.Permissions.CanSendPrivateMessage);
    }

    [Fact]
    public async Task PrivateDetails_DefaultHiddenAndOnlyPublicEmailIsOptIn()
    {
        Guid id;
        await using (var seed = _factory.CreateContext())
        {
            id = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "private@test.com");
        }

        await using (var context = _factory.CreateContext())
        {
            var profiles = Profiles(context);
            Assert.Equal(new UserProfiles.Details(null, true), await profiles.GetDetails(id, default));
            Assert.Null((await profiles.GetPublic(id, default))!.PrimaryEmail);
            Assert.True(await profiles.SetDetails(id, new(new DateOnly(2000, 2, 29), false), default));
        }

        await using (var context = _factory.CreateContext())
        {
            var profiles = Profiles(context);
            Assert.Equal(new UserProfiles.Details(new DateOnly(2000, 2, 29), false), await profiles.GetDetails(id, default));
            Assert.Equal("private@test.com", (await profiles.GetPublic(id, default))!.PrimaryEmail);
            var moduleProfile = Assert.Single(await profiles.Find([id]));
            Assert.Equal(id.ToUserLabel("Test User"), moduleProfile.DisplayName);
            var json = JsonSerializer.Serialize(moduleProfile);
            Assert.DoesNotContain("private@test.com", json);
            Assert.DoesNotContain("Birthday", json);
            Assert.True(await profiles.SetDetails(id, new(null, true), default));
            Assert.Null((await profiles.GetPublic(id, default))!.PrimaryEmail);
            Assert.Null(await profiles.GetPublic(Guid.NewGuid(), default));
        }
    }

    [Fact]
    public async Task BirthdayChanges_AreMaskedInTheAuditLog()
    {
        Guid id;
        await using (var seed = _factory.CreateContext())
        {
            id = await DatabaseSeedHelper.SeedHumanUserAsync(seed);
        }

        await using (var write = _factory.CreateAuditedContext(AuditActor.ForUser(id), Clock))
        {
            Assert.True(await Profiles(write).SetDetails(id, new(new DateOnly(1980, 5, 6), false), default));
        }

        await using var read = _factory.CreateContext();
        var entry = Assert.Single(await read.AuditLogEntries.ToListAsync());
        var difference = Assert.Single(Assert.IsType<AuditLogEntry.Updated>(entry.Action).Differences,
            difference => difference.PropertyName == nameof(HumanUser.Birthday));
        Assert.Equal(SensibleExtensions.MaskedValue, difference.OldValue?.ToString());
        Assert.Equal(SensibleExtensions.MaskedValue, difference.NewValue?.ToString());
    }

    [Fact]
    public async Task OwnDetails_RequireSessionValidateFutureDatesAndPersist()
    {
        await using var context = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(context);
        var profiles = Profiles(context);
        Assert.IsType<UnauthorizedHttpResult>(await PublicProfileEndpoints.GetDetails(new DefaultHttpContext(), _sessions, profiles));
        var http = SignedIn(id);
        Assert.IsType<BadRequest>(await PublicProfileEndpoints.SetDetails(
            new(new DateOnly(2026, 11, 13), true), http, _sessions, profiles, Clock));
        Assert.IsType<BadRequest>(await PublicProfileEndpoints.SetDetails(
            new(null, null), http, _sessions, profiles, Clock));
        Assert.IsType<NoContent>(await PublicProfileEndpoints.SetDetails(
            new(new DateOnly(1990, 1, 2), false), http, _sessions, profiles, Clock));
        var result = Assert.IsType<JsonHttpResult<UserProfiles.Details>>(
            await PublicProfileEndpoints.GetDetails(http, _sessions, profiles));
        Assert.Equal(new UserProfiles.Details(new DateOnly(1990, 1, 2), false), result.Value);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.True(context.Model.FindEntityType(typeof(HumanUser))!
            .FindProperty(nameof(HumanUser.Birthday))!.PropertyInfo!.IsDefined(typeof(SensibleAttribute), true));
    }

    [Fact]
    public async Task PublicProfile_AnonymousHasNoAdministrativeDataAndNoPrivateDetails()
    {
        await using var context = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(context, "never-label@test.com");
        await Profiles(context).SetDetails(id, new(new DateOnly(1980, 5, 6), true), default);
        var http = new DefaultHttpContext();
        var response = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(id, http, context)).Value!;
        Assert.Equal(id, response.Id);
        Assert.Equal(id.ToUserLabel("Test User"), response.DisplayName);
        Assert.Null(response.Email);
        Assert.Null(response.RegisteredAt);
        Assert.Null(response.LastLoginAt);
        Assert.Empty(response.Roles);
        Assert.Null(response.Ban);
        Assert.Null(response.Moderation);
        Assert.Equal(new(false, false), response.Permissions);
        Assert.DoesNotContain("Birthday", JsonSerializer.Serialize(response));
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        await _users.DidNotReceiveWithAnyArgs().TryFindUser(default, default, default, default);
        Assert.IsType<NotFound>(await GetProfile(Guid.NewGuid(), http, context));
    }

    [Fact]
    public async Task PublicProfile_ShowsRegistrationLoginAndRolesButNotImpersonationLogin()
    {
        await using var context = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(context);
        var impersonatorId = await DatabaseSeedHelper.SeedHumanUserAsync(context, "actor@test.com");
        var registeredAt = Now.AddDays(-30);
        var lastLoginAt = Now.AddDays(-2);
        var impersonationAt = Now.AddDays(-1);
        context.AuditLogEntries.Add(new AuditLogEntry(
            nameof(User), id.ToString().ToLowerInvariant(), new AuditLogEntry.Created(), registeredAt));
        var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(context, "moderator");
        await DatabaseSeedHelper.AssignRoleToUserAsync(context, id, roleId);
        var sessions = new SessionCommands(context);
        await sessions.CreateForUserId(id, SessionAt(lastLoginAt));
        await sessions.CreateImpersonation(impersonatorId, id, SessionAt(impersonationAt));

        var http = new DefaultHttpContext();
        var response = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(id, http, context)).Value!;

        Assert.Equal(registeredAt, response.RegisteredAt);
        Assert.Equal(lastLoginAt, response.LastLoginAt);
        Assert.Equal(["moderator"], response.Roles);
    }

    private static ValidSession SessionAt(DateTimeOffset at) =>
        new(
            AccessToken.Create(new GuestUser(None), at, TimeSpan.FromMinutes(15)),
            RefreshToken.Create(new GuestUser(None), at, TimeSpan.FromDays(7)),
            "ip",
            "agent");

    [Theory]
    [InlineData(false, false, false, true, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, false, true, true, false)]
    public async Task PublicPermissions_ReuseModerationAndImpersonationAuthority(
        bool self, bool banned, bool impersonated, bool moderate, bool impersonate)
    {
        await using var context = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(context);
        var actorId = self ? id : Guid.NewGuid();
        var http = SignedIn(actorId, impersonated);
        _access.GetUserAccess(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([], [new Grant("User.Manage"), new Grant("Impersonation.Manage")], []));
        _access.GetUserAccess(id, Arg.Any<CancellationToken>()).Returns(new UserAccess([], [], []));
        if (self)
        {
            _access.GetUserAccess(actorId, Arg.Any<CancellationToken>())
                .Returns(new UserAccess([], [new Grant("User.Manage"), new Grant("Impersonation.Manage")], []));
        }

        _users.TryFindUser(id, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(id, "Test User", ["private@test.com"], [],
                banned ? Some(new IWallOfShameQueries.ActiveBan("reason", None)) : None)));
        var response = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(id, http, context)).Value!;
        Assert.Equal(new(moderate, impersonate), response.Permissions);
        Assert.Equal(banned ? new BanInfo("reason", null) : null, response.Ban);
        Assert.NotNull(response.Moderation);
    }

    [Fact]
    public async Task ManagementDetails_ShowCurrentPrivateDataAndRetainedBanHistoryOnlyToUserManagers()
    {
        Guid id;
        Guid actorId;
        await using (var seed = _factory.CreateContext())
        {
            id = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "first@test.com");
            actorId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "moderator@test.com");
            await DatabaseSeedHelper.SeedEmailAsync(seed, id, "second@test.com");
            await Profiles(seed).SetDetails(id, new(new DateOnly(1980, 5, 6), true), default);
            await new EmailCommands(seed, Clock).Confirm("first@test.com");
        }

        await using (var write = _factory.CreateAuditedContext(AuditActor.ForUser(actorId), Clock))
        {
            var bans = new WallOfShameCommands(write);
            await bans.Ban(id, actorId, new Reason("Earlier"));
            Assert.True(await bans.Unban(id));
            await bans.Ban(id, actorId, new Reason("Current"));
        }

        await using var context = _factory.CreateContext();
        _users.TryFindUser(id, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(id, "Test User", [], [],
                Some(new IWallOfShameQueries.ActiveBan("Current", None)))));
        _access.GetUserAccess(id, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([], [new Grant("News.Read")], [new Restriction("News.Write")]));
        var http = SignedIn(actorId);
        _access.GetUserAccess(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([], [new Grant("User.Manage")], []));
        var response = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(id, http, context)).Value!;
        var details = Assert.IsType<PublicProfileEndpoints.ModerationDetails>(response.Moderation);
        Assert.Equal("Test User", details.DisplayName);
        Assert.Equal(new DateOnly(1980, 5, 6), details.Birthday);
        Assert.True(details.HideEmail);
        Assert.Collection(details.Emails.OrderBy(email => email.Value),
            email => { Assert.Equal("first@test.com", email.Value); Assert.Equal(Now, email.ConfirmedAt); },
            email => { Assert.Equal("second@test.com", email.Value); Assert.Null(email.ConfirmedAt); });
        Assert.Equal(["News.Read"], details.EffectiveGrants);
        Assert.Equal(["News.Read"], details.DirectGrants);
        Assert.Equal(["News.Write"], details.DirectRestrictions);
        Assert.False(details.TotpEnabled);
        Assert.Equal(0, details.PasskeyCount);
        Assert.Equal(0, details.RecoveryCodesRemaining);
        Assert.Equal(2, details.Bans.Count);
        var current = Assert.Single(details.Bans, ban => ban.Reason == "Current");
        Assert.True(current.Active);
        Assert.False(current.Removed);
        Assert.Equal(actorId, current.GivenById);
        Assert.Equal(actorId.ToUserLabel("Test User"), current.GivenBy);
        Assert.Equal(Now, current.CreatedAt);
        var earlier = Assert.Single(details.Bans, ban => ban.Reason == "Earlier");
        Assert.False(earlier.Active);
        Assert.True(earlier.Removed);
        Assert.Equal(Now, earlier.RemovedAt);

        _access.GetUserAccess(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([], [new Grant("Impersonation.Manage")], []));
        var limited = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(id, http, context)).Value!;
        Assert.Null(limited.Moderation);
        Assert.Null(limited.Ban);
        Assert.Null(limited.Email);
        _access.GetUserAccess(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([], [new Grant("User.Manage")], [new Restriction("User.Manage")]));
        var restricted = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await GetProfile(id, http, context)).Value!;
        Assert.Null(restricted.Moderation);
        Assert.Null(restricted.Ban);
    }

    [Fact]
    public async Task ManagementDetails_UseAuditUpdateTimeAndMarkExpiredBans()
    {
        Guid id;
        Guid actorId;
        await using (var seed = _factory.CreateContext())
        {
            id = await DatabaseSeedHelper.SeedHumanUserAsync(seed);
            actorId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "actor@test.com");
        }

        await using (var write = _factory.CreateAuditedContext(AuditActor.ForUser(actorId), Clock))
        {
            Assert.True(await Profiles(write).SetDetails(id, new(new DateOnly(2000, 1, 1), false), default));
            var entry = new WallOfShameEntry(new Reason("Expired"));
            await write.WallOfShameEntries.AddAsync(entry);
            write.Entry(entry).Property<Guid>("UserId").CurrentValue = id;
            write.Entry(entry).Property<Guid>("GivenById").CurrentValue = actorId;
            write.Entry(entry).Property<DateTimeOffset?>("ExpiresAt").CurrentValue = Now.AddDays(-1);
            await write.SaveChangesAsync();
        }

        await using var read = _factory.CreateContext();
        var profiles = Profiles(read);
        Assert.Equal(Now, (await profiles.GetManagementProfile(id, default))!.UpdatedAt);
        var ban = Assert.Single(await profiles.GetBans(id, Now, default));
        Assert.Equal(Now.AddDays(-1), ban.ExpiresAt);
        Assert.False(ban.Active);
        Assert.False(ban.Removed);
        Assert.Null(ban.RemovedAt);
    }

    [Fact]
    public async Task ManagementDetails_SummarizeTwoFactorWithoutExposingCredentials()
    {
        await using var context = _factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(context);
        var actorId = Guid.NewGuid();
        var http = SignedIn(actorId);
        _access.GetUserAccess(actorId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([], [new Grant("User.Manage")], []));
        _access.GetUserAccess(id, Arg.Any<CancellationToken>()).Returns(UserAccess.Empty);
        _users.TryFindUser(id, DeletionState.Active, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(id, "Test User", [], [], None)));
        var twoFactor = Substitute.For<ITwoFactorQueries>();
        twoFactor.GetStatus(id, Arg.Any<CancellationToken>()).Returns(new ITwoFactorQueries.TwoFactorStatus(
            true, [new ITwoFactorQueries.PasskeyInfo(Guid.NewGuid(), "Private name", "credential-secret",
                "public-key-data", 12, None)], 3));

        var response = Assert.IsType<JsonHttpResult<PublicProfileEndpoints.ProfileResponse>>(
            await PublicProfileEndpoints.Get(id, http, Profiles(context), Gate(), _users, _access,
                new EmailQueries(context), twoFactor, Clock)).Value!;
        var details = Assert.IsType<PublicProfileEndpoints.ModerationDetails>(response.Moderation);
        Assert.True(details.TotpEnabled);
        Assert.Equal(1, details.PasskeyCount);
        Assert.Equal(3, details.RecoveryCodesRemaining);
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("credential-secret", json);
        Assert.DoesNotContain("public-key-data", json);
        Assert.DoesNotContain("Private name", json);
    }

    [Fact]
    public void AuthorLinks_SurvivePublicDetailManagementAndWidgetDtoMapping()
    {
        var id = new VersionedKey<Guid>(Guid.NewGuid(), 1);
        var authorId = Guid.NewGuid();
        var label = authorId.ToUserLabel("Author");
        Assert.Equal(authorId, new INewsQueries.NewsListing(
            id, "Title", "Summary", label, None, None, None, 0, false, None, None)
            { AuthorId = authorId }.ToItem().AuthorId);
        Assert.Equal(authorId, new INewsQueries.NewsDetail(
            id, "Title", "Summary", "Content", label, None, None, None, false, None, None)
            { AuthorId = authorId }.ToResponse().AuthorId);
        Assert.Equal(authorId, new INewsQueries.ManagedNewsListing(
            id.Key, 1, None, "Title", label, None, None, None, 0, None)
            { AuthorId = authorId }.ToItem(DeletionState.Active, Now).AuthorId);
        Assert.Equal(authorId, new IArticleQueries.ArticleListing(
            id, "Title", "Summary", label, None, None, None) { AuthorId = authorId }.ToItem().AuthorId);
        Assert.Equal(authorId, new IArticleQueries.ArticleDetail(
            id, "Title", "Summary", "Content", label, None, None, None)
            { AuthorId = authorId }.ToResponse().AuthorId);
        Assert.Equal(authorId, new IArticleQueries.ManagedArticleListing(
            id.Key, 1, None, "Title", label, None, None, None)
            { AuthorId = authorId }.ToItem(DeletionState.Active).AuthorId);
        Assert.Equal(authorId, new IGalleryQueries.GalleryListing(
            id, "Title", "Description", label, None, None, None, 0)
            { AuthorId = authorId }.ToItem().AuthorId);
        Assert.Equal(authorId, new IGalleryQueries.GalleryDetail(
            id, "Title", "Description", label, None, None, None, false, [])
            { AuthorId = authorId }.ToResponse().AuthorId);
        Assert.Equal(authorId, new IGalleryQueries.ManagedGalleryListing(
            id.Key, 1, None, "Title", label, None, None, None, 0)
            { AuthorId = authorId }.ToItem(DeletionState.Active).AuthorId);
        Assert.Equal(authorId, new IDownloadQueries.DownloadInfo(
            id.Key, Guid.NewGuid(), "File", "file.txt", "text/plain", 10, label, None)
            { UploadedById = authorId }.ToItem().UploadedById);
        Assert.Equal(authorId, new IWallOfShameQueries.ShamedUser(label, "reason", None)
            { Id = authorId }.ToItem().Id);
        var human = new HumanUser("Author", [new Email("private@test.com")], None);
        Assert.Equal(authorId, human.ToActor(authorId).Id);
        Assert.Equal(label, human.ToActor(authorId).Label);
        Assert.Null(new SystemUser(None).ToActor(authorId).Id);
        Assert.Null(new GuestUser(None).ToActor(authorId).Id);
        Assert.Null(new UnknownUser(authorId).ToActor(authorId).Id);
    }
}

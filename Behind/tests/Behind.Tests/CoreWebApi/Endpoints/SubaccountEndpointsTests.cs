using System.Data;
using System.Security.Claims;
using Behind.Common.Database.Modules;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Postgres;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountEndpointsTests
{
    private static (DefaultHttpContext Http, ISessionQueries Queries, Guid SessionId) AsUser(Guid actor)
    {
        var sessionId = Guid.NewGuid();
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((new ValidSession(AccessToken.Create(new GuestUser(None)),
                RefreshToken.Create(new GuestUser(None)), "127.0.0.1", "test"), actor)));
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", sessionId.ToString())], "test"))
        };
        http.Request.Headers.UserAgent = "test";
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        return (http, queries, sessionId);
    }

    internal static async Task<SubaccountEndpoints.PinProof> SetPassword(AbstractCoreContext db, Guid actor, HttpContext http)
    {
        const string password = "test-family-passphrase";
        var settings = Substitute.For<IOptionsSnapshot<AccountsConfiguration>>();
        settings.Value.Returns(new AccountsConfiguration());
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IOptionsSnapshot<AccountsConfiguration>)).Returns(settings);
        http.RequestServices = services;
        await new PasswordCommands(db).CreateFromUserId(actor, new Password(PasswordHash.Create(password), true));
        await db.SaveChangesAsync();
        return new(Password: password);
    }

    [Fact]
    public async Task PinChangesRequireProofForTargetAndPrimaryAuthority()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var outsiderId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "other@example.test");
        var parent = AsUser(parentId);
        var child = AsUser(childId);
        var parentUser = (await db.Users.FindAsync(parentId))!;
        var childUser = (await db.Users.FindAsync(childId))!;
        db.Subaccounts.Add(new Subaccount(parentUser, childUser, false));
        db.UserSwitchPins.Add(new UserSwitchPin(childUser, new PinHash(PasswordHash.Create("1234"))));
        await db.SaveChangesAsync();
        var password = await SetPassword(db, parentId, parent.Http);
        var roles = Substitute.For<IRoleCommands>();
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(new("Rejected", null, null, false, "123", null),
            parent.Http, parent.Queries, db, roles, TimeProvider.System));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(new("Rejected", null, null, false, null, "321"),
            parent.Http, parent.Queries, db, roles, TimeProvider.System));
        var edit = new SubaccountEndpoints.UpdateRequest("Rejected", null, true, "5555", false);
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(childId, edit, parent.Http, parent.Queries, db, TimeProvider.System));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(childId, edit with { Pin = null, ClearPin = true },
            parent.Http, parent.Queries, db, TimeProvider.System));
        Assert.NotEqual("Rejected", childUser.DisplayName);

        var change = new SubaccountEndpoints.PinRequest("5678", false, new(CurrentPin: "1234"));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(parentId, change, parent.Http, parent.Queries, db));
        Assert.IsType<NotFound>(await SubaccountEndpoints.ChangePin(outsiderId, change, parent.Http, parent.Queries, db));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(childId, change, child.Http, child.Queries, db));
        Assert.IsType<NoContent>(await SubaccountEndpoints.ChangePin(childId, change, parent.Http, parent.Queries, db));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(childId, change, parent.Http, parent.Queries, db));
        var remove = new SubaccountEndpoints.PinRequest(null, true, new(Password: "wrong"));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(childId, remove, parent.Http, parent.Queries, db));
        Assert.IsType<NoContent>(await SubaccountEndpoints.ChangePin(childId, remove with { Proof = password }, parent.Http, parent.Queries, db));
        Assert.False(await db.UserSwitchPins.AnyAsync());
    }

    [Fact]
    public async Task BrowsingPagesOnlyEligibleIndependentUsersWithPublicLabels()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var requester = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db));
        var first = new HumanUser("Candidate A", [new Email("first@example.test")], None);
        var second = new HumanUser("Candidate B", [new Email("second@example.test")], None);
        var child = new HumanUser("Candidate child", [new Email("child@example.test")], None);
        var parent = new HumanUser("Candidate parent", [new Email("parent@example.test")], None);
        var emailLess = new HumanUser { DisplayName = "Candidate without email" };
        db.Users.AddRange(first, second, child, parent, emailLess);
        db.Subaccounts.Add(new Subaccount(parent, child, true));
        await db.SaveChangesAsync();

        async Task<Guid[]> Page(int page)
        {
            var response = await SubaccountLinkEndpoints.Candidates(" CANDIDATE ", requester.Http, requester.Queries, db, page, 1);
            var result = Assert.IsType<Ok<IEnumerable<SubaccountLinkEndpoints.Candidate>>>(response);
            var rows = result.Value!.ToArray();
            Assert.All(rows, row => Assert.DoesNotContain("@", row.DisplayName));
            return rows.Select(row => row.UserId).ToArray();
        }

        Assert.Equal([db.GetKey(first)], await Page(1));
        Assert.Equal([db.GetKey(second)], await Page(2));
        Assert.Empty(await Page(3));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(db.GetKey(first), requester.Http, requester.Queries, db));
        Assert.Equal([db.GetKey(second)], await Page(1));
    }

    [Fact]
    public async Task OnlyIntendedChildCanAcceptCurrentInvitationAndReleasePreservesAccount()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@test.com");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@test.com");
        var otherId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "other@test.com");
        var parent = AsUser(parentId);
        var child = AsUser(childId);
        var other = AsUser(otherId);
        var security = Options.Create(new SecurityConfiguration(false, None, None, None, EmailConfirmation: false));
        var requireConfirmed = Options.Create(new SecurityConfiguration());

        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var repository = db.GetRepository<HumanUser, Guid>();
        var updated = (await repository.TryFindAsync(childId)).GetOrElse(new HumanUser());
        repository.Update(childId, updated with { DisplayName = "Updated name" });
        await db.SaveChangesAsync();
        Assert.Empty(await db.Subaccounts.ToListAsync());
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.RequestLink(childId, other.Http, other.Queries, db));
        var pending = Assert.IsType<Ok<List<SubaccountLinkEndpoints.Invitation>>>(
            await SubaccountLinkEndpoints.Requests(child.Http, child.Queries, db));
        var invitation = Assert.Single(pending.Value!);
        Assert.Equal(parentId, invitation.UserId);
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(invitation.RequestId, parent.Http, parent.Queries, db));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Decline(invitation.RequestId, child.Http, child.Queries, db));
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(invitation.RequestId, child.Http, child.Queries, db));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var current = (await db.Users.FindAsync(childId))!.PendingLinkRequestId!.Value;
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Confirm(current, child.Http, child.Queries, db));
        Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.RequestLink(otherId, child.Http, child.Queries, db));
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.RequestLink(parentId, other.Http, other.Queries, db));
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Release(childId, other.Http, other.Queries, db, TimeProvider.System, security));
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.Release(childId, parent.Http, parent.Queries, db, TimeProvider.System, requireConfirmed));
        var commands = new SessionCommands(db);
        var queries = new SessionQueries(db);
        var user = (await db.Users.FindAsync(childId))!;
        var switchedSession = await commands.CreateForUserId(childId,
            new ValidSession(AccessToken.Create(user), RefreshToken.Create(user), "127.0.0.1", "test") { FamilyPrimaryUserId = parentId });
        var directSession = await commands.CreateForUserId(childId,
            new ValidSession(AccessToken.Create(user), RefreshToken.Create(user), "127.0.0.1", "test"));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Release(childId, parent.Http, parent.Queries, db, TimeProvider.System, security));
        Assert.True(await queries.TryFindById(switchedSession) == None);
        Assert.True((await queries.TryFindById(directSession)).Match(() => false, session => session is ValidSession));
        Assert.False(await db.Subaccounts.AnyAsync());
        Assert.True(await db.Emails.AnyAsync(email => email.Value == "child@test.com"));
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(current, child.Http, child.Queries, db));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var next = (await db.Users.FindAsync(childId))!.PendingLinkRequestId!.Value;
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Confirm(next, child.Http, child.Queries, db));
        Assert.True((await queries.TryFindById(switchedSession)).Match(() => false, session => session is InvalidSession));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FeatureGatesCheckBothFlagsForRelationshipOperations(bool subaccounts, bool linking)
    {
        var settings = new SecurityConfiguration { Accounts = new() { Subaccounts = subaccounts, LinkUnlink = linking } };
        using var services = new ServiceCollection().AddSingleton(Options.Create(settings)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var context = new DefaultEndpointFilterInvocationContext(http, []);
        foreach (var requiresLinking in new[] { false, true })
        {
            var result = await new SubaccountFeatureFilter(requiresLinking)
                .InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.NoContent()));
            Assert.Equal(subaccounts && (!requiresLinking || linking) ? 204 : 404,
                Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DisabledFeatureLeavesPendingConsentUntouched(bool subaccounts, bool linking)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parent = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test"));
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var child = AsUser(childId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var pending = await db.Users.Select(user => user.PendingLinkRequestId).SingleAsync(id => id != null);
        var settings = new SecurityConfiguration { Accounts = new() { Subaccounts = subaccounts, LinkUnlink = linking } };
        using var services = new ServiceCollection().AddSingleton(Options.Create(settings)).BuildServiceProvider();
        child.Http.RequestServices = services;
        var invocation = new DefaultEndpointFilterInvocationContext(child.Http, []);
        var result = await new SubaccountFeatureFilter(true).InvokeAsync(invocation,
            async _ => await SubaccountLinkEndpoints.Confirm(pending!.Value, child.Http, child.Queries, db));
        Assert.IsType<NotFound>(result);
        Assert.False(await db.Subaccounts.AnyAsync());
        Assert.True(await db.Users.AnyAsync(user => user.PendingLinkRequestId == pending));
        using var enabled = new ServiceCollection().AddSingleton(Options.Create(new SecurityConfiguration
        {
            Accounts = new() { LinkUnlink = true }
        })).BuildServiceProvider();
        child.Http.RequestServices = enabled;
        Assert.IsType<NoContent>(await new SubaccountFeatureFilter(true).InvokeAsync(invocation,
            async _ => await SubaccountLinkEndpoints.Confirm(pending!.Value, child.Http, child.Queries, db)));
        Assert.True(await db.Subaccounts.AnyAsync(link => EF.Property<Guid>(link, "UserId") == childId));
        Assert.False(await db.Users.AnyAsync(user => user.PendingLinkRequestId == pending));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresOverlappingRequestSnapshotsCannotReplaceWinningParent()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(container.GetConnectionString()).Options;
        await using var setup = new CoreContextPostgres(options);
        await setup.Database.MigrateAsync();
        var first = new HumanUser("First", [new Email("first@test.com")], None);
        var second = new HumanUser("Second", [new Email("second@test.com")], None);
        var child = new HumanUser("Child", [new Email("child@test.com")], None);
        setup.Users.AddRange(first, second, child);
        await setup.SaveChangesAsync();
        var firstId = setup.GetKey(first);
        var childId = setup.GetKey(child);
        var winner = AsUser(firstId);
        var loser = AsUser(setup.GetKey(second));
        await using var left = new CoreContextPostgres(options);
        await using var right = new CoreContextPostgres(options);
        await using var firstTransaction = await left.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var staleTransaction = await right.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Assert.Null((await right.Users.FindAsync(childId))!.PendingLinkRequestId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, winner.Http, winner.Queries, left));
        await firstTransaction.CommitAsync();
        var error = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            SubaccountLinkEndpoints.RequestLink(childId, loser.Http, loser.Queries, right));
        Assert.Equal("40001", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        await staleTransaction.RollbackAsync();
        setup.ChangeTracker.Clear();
        Assert.Equal(firstId, (await setup.Users.FindAsync(childId))!.PendingParentId);
        Assert.False(await setup.Subaccounts.AnyAsync());
    }

    [Fact]
    public async Task EmailRemovedBeforeConsentPreventsLinkAndEmailLessChildCannotBeReleased()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db);
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@test.com");
        var parent = AsUser(parentId);
        var child = AsUser(childId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var user = (await db.Users.FindAsync(childId))!;
        var requestId = user.PendingLinkRequestId!.Value;
        db.Emails.Remove(await db.Emails.SingleAsync(address => address.Value == "child@test.com"));
        await db.SaveChangesAsync();
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.Confirm(requestId, child.Http, child.Queries, db));
        Assert.False(await db.Subaccounts.AnyAsync());
        db.Subaccounts.Add(new Subaccount((await db.Users.FindAsync(parentId))!, user, true));
        await db.SaveChangesAsync();
        var security = Options.Create(new SecurityConfiguration(false, None, None, None, EmailConfirmation: false));
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.Release(childId, parent.Http, parent.Queries, db, TimeProvider.System, security));
        Assert.True(await db.Subaccounts.AnyAsync());
    }

    [Fact]
    public async Task CreatingAChildDiscardsAnIncomingInvitation()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var proposer = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db, "proposer@example.test"));
        var recipientId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "recipient@example.test");
        var recipient = AsUser(recipientId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(recipientId, proposer.Http, proposer.Queries, db));
        var requestId = await db.Users.Select(user => user.PendingLinkRequestId).SingleAsync(id => id != null);
        var created = await SubaccountEndpoints.Create(new("New child", null, null, true, null),
            recipient.Http, recipient.Queries, db, Substitute.For<IRoleCommands>(), TimeProvider.System);
        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(created).StatusCode);
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(requestId!.Value, recipient.Http, recipient.Queries, db));
    }

    [Fact]
    public async Task ConcurrentPrimaryPinSetupPreservesWinningPinAndRollsBackNewChild()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = await DatabaseSeedHelper.SeedHumanUserAsync(db);
        var parent = AsUser(primary);
        var roles = Substitute.For<IRoleCommands>();
        var proof = await SetPassword(db, primary, parent.Http);
        var winningHash = PasswordHash.Create("000789").ToString();
        db.SavingChanges += (_, _) => db.Database.ExecuteSql(
            $"INSERT INTO UserSwitchPins (UserId, PinHash) VALUES ({primary}, {winningHash})");

        var response = await SubaccountEndpoints.Create(new("Child", null, null, true, "001234", "002345", proof),
            parent.Http, parent.Queries, db, roles, TimeProvider.System);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
        await using var verified = factory.CreateContext();
        Assert.Equal(winningHash, (await verified.UserSwitchPins.SingleAsync()).PinHash.ToString());
        Assert.False(await verified.Subaccounts.AnyAsync());
        Assert.False(await verified.Users.AnyAsync(user => user.DisplayName == "Child"));
    }

    [Fact]
    public async Task SqliteConcurrentFirstChildPinUpdatePreservesWinningPinAndProfile()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        await VerifyConcurrentFirstChildPinUpdate(db, factory.CreateContext);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresConcurrentFirstChildPinUpdatePreservesWinningPinAndProfile()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new CoreContextPostgres(options);
        await db.Database.MigrateAsync();
        await VerifyConcurrentFirstChildPinUpdate(db, () => new CoreContextPostgres(options));
    }

    private static async Task VerifyConcurrentFirstChildPinUpdate(AbstractCoreContext db, Func<AbstractCoreContext> createContext)
    {
        var primary = new HumanUser("Parent", [new Email("parent@example.test")], None);
        var child = new HumanUser("Child", [new Email("child@example.test")], None);
        db.Subaccounts.Add(new Subaccount(primary, child, true));
        await db.SaveChangesAsync();
        var parent = AsUser(db.GetKey(primary));
        var childId = db.GetKey(child);
        var proof = await SetPassword(db, db.GetKey(primary), parent.Http);
        var winningHash = PasswordHash.Create("000789").ToString();
        db.SavingChanges += (_, _) => db.Database.ExecuteSql(
            $"""INSERT INTO "UserSwitchPins" ("UserId", "PinHash") VALUES ({childId}, {winningHash})""");

        var response = await SubaccountEndpoints.Update(childId, new("Losing name", null, false, "001234", false, proof),
            parent.Http, parent.Queries, db, TimeProvider.System);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
        await using var verified = createContext();
        Assert.Equal(winningHash, (await verified.UserSwitchPins.SingleAsync()).PinHash.ToString());
        Assert.Equal("Child", (await verified.Users.FindAsync(childId))!.DisplayName);
        Assert.True((await verified.Subaccounts.SingleAsync()).IsChild);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("000123", "child@example.test")]
    public async Task LinkedUserCanBeCreatedSwitchedAndManagedOnlyByPrimary(string? pin, string? email)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var parent = AsUser(primary);
        var roles = Substitute.For<IRoleCommands>();
        var birthday = new DateOnly(2018, 2, 2);
        var proof = await SetPassword(db, primary, parent.Http);
        var request = new SubaccountEndpoints.CreateRequest("Child", email, birthday, true, pin, Proof: proof);
        await SubaccountEndpoints.Create(request, parent.Http, parent.Queries, db, roles, TimeProvider.System);
        db.ChangeTracker.Clear();
        var link = await db.Subaccounts
            .Include(account => account.PrimaryUser)
            .Include(account => account.User)
            .SingleAsync();
        var childId = db.GetKey(link.User);
        Assert.Equal(primary, db.GetKey(link.PrimaryUser));
        Assert.NotEqual(primary, childId);
        var credential = await db.UserSwitchPins.SingleOrDefaultAsync();
        Assert.Equal(pin is not null, credential is not null);
        if (pin is not null)
        {
            var storedHash = Assert.IsType<PinHash>(credential!.PinHash);
            Assert.NotEqual(pin, storedHash.ToString());
            Assert.True(PasswordHash.CreateFromExisting(storedHash.ToString()).Verify(pin));
        }
        var profiles = new UserProfiles(db, Substitute.For<IModuleFiles>());
        await profiles.SetDetails(childId, new(new DateOnly(1980, 1, 1), false), default);
        Assert.Equal(new UserProfiles.Details(birthday, false, true), await profiles.GetDetails(childId, default));
        await SubaccountEndpoints.List(parent.Http, parent.Queries, db, Options.Create(new SecurityConfiguration()));

        var child = AsUser(childId);
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(request, child.Http, child.Queries, db, roles, TimeProvider.System));
        var other = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db, "elsewhere@example.test"));
        var update = new SubaccountEndpoints.UpdateRequest("Different", birthday, false, null, true, proof);
        Assert.IsType<NotFound>(await SubaccountEndpoints.Update(childId, update, other.Http, other.Queries, db, TimeProvider.System));

        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(childId.ToString()).Returns(new ApplicationUser { Id = childId });
        var security = Options.Create(new SecurityConfiguration(false, None, Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var commands = Substitute.For<ISessionCommands>();
        Task<IResult> Switch(string? suppliedPin) => SubaccountEndpoints.Switch(childId, new(suppliedPin),
            parent.Http, parent.Queries, db, manager, security, commands, Substitute.For<IWallOfShameQueries>(),
            TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);

        if (pin is not null)
        {
            Assert.IsType<ForbidHttpResult>(await Switch(null));
            Assert.IsType<ForbidHttpResult>(await Switch("000000"));
            await commands.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
        }
        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(pin));
        await commands.Received(1).CreateForUserId(childId, Arg.Is<ValidSession>(s => s.FamilyPrimaryUserId == primary), Arg.Any<CancellationToken>());
        await commands.Received(1).InvalidateSession(parent.SessionId, primary, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        Assert.IsType<NoContent>(await SubaccountEndpoints.Update(childId, update, parent.Http, parent.Queries, db, TimeProvider.System));
        Assert.False(await db.UserSwitchPins.AnyAsync());
        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(null));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountSessionTests
{
    [Fact]
    public async Task PrimaryCanSwitchToLinkedProfilesWithoutPins()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        await VerifyPrimarySwitchingWithoutPins(db);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresPrimaryCanSwitchToLinkedProfilesWithoutPins()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new CoreContextPostgres(options);
        await db.Database.MigrateAsync();
        await VerifyPrimarySwitchingWithoutPins(db);
    }

    private static async Task VerifyPrimarySwitchingWithoutPins(AbstractCoreContext db)
    {
        var primary = new HumanUser { DisplayName = "Primary" };
        var child = new HumanUser { DisplayName = "Child" };
        var adult = new HumanUser { DisplayName = "Adult" };
        var protectedChild = new HumanUser { DisplayName = "Protected" };
        var outsider = new HumanUser { DisplayName = "Outsider" };
        db.Users.AddRange(primary, child, adult, protectedChild, outsider);
        db.Subaccounts.AddRange(new Subaccount(primary, child, true), new Subaccount(primary, adult, false),
            new Subaccount(primary, protectedChild, true));
        await db.SaveChangesAsync();

        var primaryId = db.GetKey(primary);
        var childId = db.GetKey(child);
        var adultId = db.GetKey(adult);
        var protectedId = db.GetKey(protectedChild);
        var queries = new SessionQueries(db);
        var commands = new SessionCommands(db);
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        foreach (var user in new[] { primary, child, adult, protectedChild })
        {
            var id = db.GetKey(user);
            manager.FindByIdAsync(id.ToString()).Returns(new ApplicationUser { Id = id, DisplayName = user.DisplayName });
        }
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));

        Task<Guid> Start(HumanUser user) => commands.CreateForUserId(db.GetKey(user),
            new ValidSession(AccessToken.Create(user), RefreshToken.Create(user), "127.0.0.1", "test"));

        async Task<JsonElement[]> Targets(Guid sessionId)
        {
            var result = await SubaccountEndpoints.List(SessionTestHelper.SignedIn(sessionId), queries, db, security);
            return JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(result).Value)
                .GetProperty("SwitchAccounts").EnumerateArray().ToArray();
        }

        Task<IResult> Switch(Guid sessionId, Guid destinationId, string? pin = null)
        {
            var http = SessionTestHelper.SignedIn(sessionId);
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            http.Request.Headers.UserAgent = "test";
            return SubaccountEndpoints.Switch(destinationId, new(pin), http, queries, db, manager, security, commands,
                new WallOfShameQueries(db), TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);
        }

        async Task<Guid> Enter(Guid sessionId, Guid destinationId, string? pin = null)
        {
            var result = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(sessionId, destinationId, pin));
            var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!.AccessToken);
            var next = Guid.Parse(token.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
            Assert.True((await queries.TryFindById(sessionId)).Match(() => false, session => session is InvalidSession));
            Assert.True((await queries.TryFindByIdWithUserId(next)).Match(() => false,
                session => session.UserId == destinationId && session.Session is ValidSession { FamilyPrimaryUserId: var anchor } && anchor == primaryId));
            return next;
        }

        var primarySession = await Start(primary);
        var targets = await Targets(primarySession);
        Assert.Equal(new[] { adultId, childId, protectedId }.Order(),
            targets.Select(target => target.GetProperty("UserId").GetGuid()).Order());
        Assert.All(targets, target => Assert.False(target.GetProperty("HasPin").GetBoolean()));
        db.UserSwitchPins.Add(new UserSwitchPin(primary, new PinHash(PasswordHash.Create("0001"))));
        db.UserSwitchPins.Add(new UserSwitchPin(protectedChild, new PinHash(PasswordHash.Create("0002"))));
        await db.SaveChangesAsync();
        targets = await Targets(primarySession);
        Assert.Equal(new[] { adultId, childId, protectedId }.Order(),
            targets.Select(target => target.GetProperty("UserId").GetGuid()).Order());
        Assert.All(targets, target => Assert.Equal(target.GetProperty("UserId").GetGuid() == protectedId,
            target.GetProperty("HasPin").GetBoolean()));
        Assert.IsType<NotFound>(await Switch(primarySession, db.GetKey(outsider)));
        Assert.IsType<ForbidHttpResult>(await Switch(primarySession, protectedId));
        Assert.IsType<ForbidHttpResult>(await Switch(primarySession, protectedId, "wrong"));

        var childSession = await Enter(primarySession, childId);
        Assert.Equal(new[] { primaryId, protectedId }.Order(),
            (await Targets(childSession)).Select(target => target.GetProperty("UserId").GetGuid()).Order());
        Assert.IsType<ForbidHttpResult>(await Switch(childSession, adultId));
        Assert.IsType<ForbidHttpResult>(await Switch(childSession, primaryId));
        var returnedPrimary = await Enter(childSession, primaryId, "0001");
        Assert.Contains(await Targets(returnedPrimary), target => target.GetProperty("UserId").GetGuid() == adultId
            && !target.GetProperty("HasPin").GetBoolean());
        await Enter(returnedPrimary, adultId);

        var directChild = await Start(child);
        Assert.IsType<ForbidHttpResult>(await Switch(directChild, primaryId));
        Assert.IsType<NotFound>(await Switch(directChild, adultId));
        await Enter(await Start(primary), protectedId, "0002");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("000789")]
    public async Task FamilySessionsCanReturnToPrimaryOnlyWithConfiguredPin(string? primaryPin)
    {
        const string firstPin = "000123";
        const string secondPin = "000456";
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var primaryUser = await db.Users.FindAsync(primary) ?? throw new InvalidOperationException();
        var first = new HumanUser { DisplayName = "First", Emails = [new Email("first@example.test")] };
        db.Users.Add(first);
        var firstId = db.GetKey(first);
        db.Subaccounts.Add(new Subaccount(primaryUser, first, true));
        db.UserSwitchPins.Add(new UserSwitchPin(first, new PinHash(PasswordHash.Create(firstPin))));
        await db.SaveChangesAsync();
        var commands = new SessionCommands(db);
        var queries = new SessionQueries(db);
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(firstId.ToString()).Returns(new ApplicationUser { Id = firstId, DisplayName = "First" });
        manager.FindByIdAsync(primary.ToString()).Returns(new ApplicationUser { Id = primary, DisplayName = "Primary" });
        var jwt = new JwtSettings("issuer", "audience", new string('k', 64));
        var security = Options.Create(new SecurityConfiguration(false, None, Some(jwt), None) { Accounts = new() { LinkUnlink = true } });
        Task<Guid> Start(Guid userId) => commands.CreateForUserId(userId,
            new ValidSession(AccessToken.Create(first), RefreshToken.Create(first), "127.0.0.1", "test"));
        Task<IResult> Switch(Guid sessionId, Guid target, string? pin = null)
        {
            var http = SessionTestHelper.SignedIn(sessionId);
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            http.Request.Headers.UserAgent = "test";
            return SubaccountEndpoints.Switch(target, new(pin), http, queries, db, manager, security, commands,
                new WallOfShameQueries(db), TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);
        }

        var parentSession = await Start(primary);
        var parentHttp = SessionTestHelper.SignedIn(parentSession);
        var proof = await SubaccountEndpointsTests.SetPassword(db, primary, parentHttp);
        var created = await SubaccountEndpoints.Create(new("Second", null, null, true, secondPin, primaryPin, proof),
            parentHttp, queries, db, Substitute.For<IRoleCommands>(), TimeProvider.System);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<IStatusCodeHttpResult>(created).StatusCode);
        var newAccount = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(created).Value);
        var secondId = newAccount.GetProperty("UserId").GetGuid();
        if (primaryPin is not null)
        {
            var replacement = await SubaccountEndpoints.Create(new("Third", null, null, true, null, "000111", proof),
                parentHttp, queries, db, Substitute.For<IRoleCommands>(), TimeProvider.System);
            Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(replacement).StatusCode);
        }
        manager.FindByIdAsync(secondId.ToString()).Returns(new ApplicationUser { Id = secondId, DisplayName = "Second" });

        async Task<JsonElement> List(Guid sessionId)
        {
            var response = await SubaccountEndpoints.List(SessionTestHelper.SignedIn(sessionId), queries, db, security);
            return JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(response).Value);
        }
        Assert.Equal(primaryPin is not null, (await List(parentSession)).GetProperty("PrimaryHasPin").GetBoolean());
        Assert.True((await List(parentSession)).GetProperty("LinkUnlinkEnabled").GetBoolean());
        var direct = await Start(firstId);
        var directTargets = (await List(direct)).GetProperty("SwitchAccounts").EnumerateArray();
        Assert.Equal(primaryPin is not null, directTargets.Any(item => item.GetProperty("UserId").GetGuid() == primary));
        Assert.IsType<NotFound>(await Switch(direct, secondId, secondPin));

        var entered = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(parentSession, firstId, firstPin));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(entered.Value!.AccessToken);
        var switchedSession = Guid.Parse(token.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        var disabled = new SessionQueries(db, Options.Create(new SecurityConfiguration { Accounts = new() { Subaccounts = false } }));
        Assert.True(await disabled.TryFindById(switchedSession) == None);
        Assert.True(await disabled.TryFindById(direct) != None);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "email");
        Assert.True((await queries.TryFindById(parentSession)).Match(() => false, value => value is InvalidSession));
        var targets = (await List(switchedSession)).GetProperty("SwitchAccounts").EnumerateArray().Select(item => item.GetProperty("UserId").GetGuid()).ToArray();
        Assert.Contains(secondId, targets);
        Assert.Equal(primaryPin is not null, targets.Contains(primary));
        var outsider = await DatabaseSeedHelper.SeedHumanUserAsync(db, "outsider@example.test");
        Assert.IsType<NotFound>(await Switch(switchedSession, outsider, primaryPin));
        Assert.IsType<ForbidHttpResult>(await Switch(switchedSession, primary));
        Assert.IsType<ForbidHttpResult>(await Switch(switchedSession, primary, "000111"));
        var sibling = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(switchedSession, secondId, secondPin));
        var siblingToken = new JwtSecurityTokenHandler().ReadJwtToken(sibling.Value!.AccessToken);
        var siblingSession = Guid.Parse(siblingToken.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        Assert.True((await queries.TryFindByIdWithUserId(siblingSession)).Match(() => false,
            value => value.UserId == secondId && value.Session is ValidSession { FamilyPrimaryUserId: var familyId } && familyId == primary));
        if (primaryPin is not null)
        {
            var returned = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(siblingSession, primary, primaryPin));
            var returnedToken = new JwtSecurityTokenHandler().ReadJwtToken(returned.Value!.AccessToken);
            var returnedSession = Guid.Parse(returnedToken.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
            var returnedFamily = await List(returnedSession);
            Assert.True(returnedFamily.GetProperty("IsPrimary").GetBoolean());
            Assert.True(returnedFamily.GetProperty("CanBrowseAndRelease").GetBoolean());
            Assert.False(returnedFamily.GetProperty("LinkUnlinkEnabled").GetBoolean());
            var returnedHttp = SessionTestHelper.SignedIn(returnedSession);
            var browse = Assert.IsType<Ok<IEnumerable<SubaccountLinkEndpoints.Candidate>>>(
                await SubaccountLinkEndpoints.Candidates(null, returnedHttp, queries, db));
            Assert.Equal(outsider, Assert.Single(browse.Value!).UserId);
            var directParent = await Start(primary);
            var parallelParent = await Start(primary);
            var parallelSwitch = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(parallelParent, secondId, secondPin));
            var parallelToken = new JwtSecurityTokenHandler().ReadJwtToken(parallelSwitch.Value!.AccessToken);
            var parallelSession = Guid.Parse(parallelToken.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
            var release = await SubaccountLinkEndpoints.Release(firstId, returnedHttp, queries, db,
                TimeProvider.System, Options.Create(security.Value with { EmailConfirmation = false }));
            Assert.IsType<NoContent>(release);
            Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.List(returnedHttp, queries, db, security));
            Assert.IsType<ForbidHttpResult>(await Switch(returnedSession, secondId));
            Assert.True((await queries.TryFindById(returnedSession)).Match(() => false, value => value is InvalidSession));
            Assert.True((await queries.TryFindById(parallelSession)).Match(() => false, value => value is InvalidSession));
            Assert.True((await List(directParent)).GetProperty("IsPrimary").GetBoolean());
            Assert.True((await queries.TryFindById(direct)).Match(() => false, value => value is ValidSession));
        }
    }

    [Fact]
    public async Task SharedChildSwitchesToEachConsentingAdultWithScopedAccessAndIndependentRelease()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        await VerifySharedChildSwitching(db);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresSharedChildSwitchScopeAndRelease()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new CoreContextPostgres(options);
        await db.Database.MigrateAsync();
        await VerifySharedChildSwitching(db);
    }

    private static async Task VerifySharedChildSwitching(AbstractCoreContext db)
    {
        var firstAdult = new HumanUser("First adult", [new Email("first@example.test")], None);
        var secondAdult = new HumanUser("Second adult", [new Email("second@example.test")], None);
        var sharedChild = new HumanUser("Shared child", [new Email("shared@example.test")], None);
        var privateChild = new HumanUser("Private child", [new Email("private@example.test")], None);
        var adultProfile = new HumanUser("Other adult", [new Email("adult@example.test")], None);
        db.Users.AddRange(firstAdult, secondAdult, sharedChild, privateChild, adultProfile);
        db.Subaccounts.AddRange(new Subaccount(firstAdult, sharedChild, true), new Subaccount(secondAdult, privateChild, true),
            new Subaccount(firstAdult, adultProfile, false));
        (HumanUser User, string Pin)[] profiles = [(firstAdult, "0001"), (secondAdult, "0002"),
            (sharedChild, "0003"), (privateChild, "0004"), (adultProfile, "0005")];
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        foreach (var (user, pin) in profiles)
        {
            db.UserSwitchPins.Add(new UserSwitchPin(user, new PinHash(PasswordHash.Create(pin))));
            var id = db.GetKey(user);
            manager.FindByIdAsync(id.ToString()).Returns(new ApplicationUser { Id = id, DisplayName = user.DisplayName });
        }
        await db.SaveChangesAsync();
        var firstId = db.GetKey(firstAdult);
        var secondId = db.GetKey(secondAdult);
        var childId = db.GetKey(sharedChild);
        var privateId = db.GetKey(privateChild);
        var adultProfileId = db.GetKey(adultProfile);

        var queries = new SessionQueries(db);
        var commands = new SessionCommands(db);
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None, EmailConfirmation: false)
        {
            Accounts = new() { LinkUnlink = true }
        });

        Task<Guid> Start(HumanUser user) => commands.CreateForUserId(db.GetKey(user),
            new ValidSession(AccessToken.Create(user), RefreshToken.Create(user), "127.0.0.1", "test"));

        Task<IResult> Switch(Guid sessionId, Guid destinationId, string pin)
        {
            var http = SessionTestHelper.SignedIn(sessionId);
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            http.Request.Headers.UserAgent = "test";
            return SubaccountEndpoints.Switch(destinationId, new(pin), http, queries, db, manager, security, commands,
                new WallOfShameQueries(db), TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);
        }

        async Task<Guid> SwitchSuccessfully(Guid sessionId, Guid destinationId, string pin)
        {
            var switched = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(sessionId, destinationId, pin));
            var token = new JwtSecurityTokenHandler().ReadJwtToken(switched.Value!.AccessToken);
            var switchedId = Guid.Parse(token.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
            Assert.True((await queries.TryFindByIdWithUserId(switchedId)).Match(() => false,
                actor => actor.UserId == destinationId && actor.Session is ValidSession));
            return switchedId;
        }

        var firstSession = await Start(firstAdult);
        var firstHttp = SessionTestHelper.SignedIn(firstSession);
        var secondHttp = SessionTestHelper.SignedIn(await Start(secondAdult));
        var childSession = await Start(sharedChild);
        var childHttp = SessionTestHelper.SignedIn(childSession);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, secondHttp, queries, db));
        var invitations = Assert.IsType<Ok<List<SubaccountLinkEndpoints.Invitation>>>(
            await SubaccountLinkEndpoints.Requests(childHttp, queries, db));
        var invitation = Assert.Single(invitations.Value!);
        Assert.Equal(secondId, invitation.UserId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Confirm(invitation.RequestId, childHttp, queries, db));

        var family = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(
            await SubaccountEndpoints.List(childHttp, queries, db, security)).Value);
        Assert.False(family.GetProperty("CanBrowseAndRelease").GetBoolean());
        var targets = family.GetProperty("SwitchAccounts").EnumerateArray().ToArray();
        Assert.Equal(new[] { firstId, secondId }.Order(), targets.Select(target => target.GetProperty("UserId").GetGuid()).Order());
        Assert.All(targets, target => Assert.False(target.GetProperty("IsChild").GetBoolean()));
        Assert.IsType<ForbidHttpResult>(await Switch(childSession, secondId, "0001"));
        Assert.IsType<NotFound>(await Switch(childSession, privateId, "0004"));
        Assert.IsType<NotFound>(await Switch(firstSession, secondId, "0002"));

        var switchedSecond = await SwitchSuccessfully(childSession, secondId, "0002");
        var restrictedHttp = SessionTestHelper.SignedIn(switchedSecond);
        var restrictedFamily = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(
            await SubaccountEndpoints.List(restrictedHttp, queries, db, security)).Value);
        Assert.Empty(restrictedFamily.GetProperty("Accounts").EnumerateArray());
        Assert.False(restrictedFamily.GetProperty("IsPrimary").GetBoolean());
        var restrictedTargets = restrictedFamily.GetProperty("SwitchAccounts").EnumerateArray().ToArray();
        Assert.Contains(restrictedTargets, target => target.GetProperty("UserId").GetGuid() == childId && target.GetProperty("IsChild").GetBoolean());
        Assert.DoesNotContain(restrictedTargets, target => target.GetProperty("UserId").GetGuid() == privateId);
        Assert.IsType<NotFound>(await Switch(switchedSecond, privateId, "0004"));
        Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Candidates(null, restrictedHttp, queries, db));
        Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Release(privateId, restrictedHttp, queries, db,
            TimeProvider.System, security));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(privateId,
            new("Updated", null, true, null, false), restrictedHttp, queries, db, TimeProvider.System));

        var returnedChild = await SwitchSuccessfully(switchedSecond, childId, "0003");
        var switchedFirst = await SwitchSuccessfully(returnedChild, firstId, "0001");
        var survivingSecond = await SwitchSuccessfully(await Start(sharedChild), secondId, "0002");
        var survivingChild = await Start(sharedChild);
        var switchedAdult = await SwitchSuccessfully(await Start(firstAdult), adultProfileId, "0005");
        await SwitchSuccessfully(switchedAdult, firstId, "0001");

        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Release(childId, firstHttp, queries, db, TimeProvider.System, security));
        Assert.True((await queries.TryFindById(switchedFirst)).Match(() => true, session => session is InvalidSession));
        Assert.True((await queries.TryFindById(survivingSecond)).Match(() => false, session => session is ValidSession));
        Assert.True((await queries.TryFindById(survivingChild)).Match(() => false, session => session is ValidSession));
        Assert.IsType<NotFound>(await Switch(survivingChild, firstId, "0001"));
        await SwitchSuccessfully(survivingChild, secondId, "0002");

        var returnedAfterRelease = await SwitchSuccessfully(survivingSecond, childId, "0003");
        var directAfterRelease = await Start(sharedChild);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Release(childId, secondHttp, queries, db, TimeProvider.System, security));
        Assert.IsType<ForbidHttpResult>(await Switch(returnedAfterRelease, secondId, "0002"));
        Assert.True((await queries.TryFindById(directAfterRelease)).Match(() => false, session => session is ValidSession));
        Assert.True(await db.UserSwitchPins.AnyAsync(pin => EF.Property<Guid>(pin, "UserId") == childId));
    }
}

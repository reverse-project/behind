using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountSessionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("000789")]
    public async Task FamilySessionsCanReturnToPrimaryOnlyWithConfiguredPin(string? primaryPin)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var primaryUser = await db.Users.FindAsync(primary) ?? throw new InvalidOperationException();
        var first = new HumanUser { DisplayName = "First", Emails = [new Email("first@example.test")] };
        db.Users.Add(first);
        var firstId = db.GetKey(first);
        db.Subaccounts.Add(new Subaccount(primaryUser, first, true));
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
        var created = await SubaccountEndpoints.Create(new("Second", null, null, true, null, primaryPin, proof),
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
        Assert.Empty((await List(direct)).GetProperty("Accounts").EnumerateArray());
        Assert.IsType<ForbidHttpResult>(await Switch(direct, primary, primaryPin));
        Assert.IsType<ForbidHttpResult>(await Switch(direct, secondId));

        var entered = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(parentSession, firstId));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(entered.Value!.AccessToken);
        var switchedSession = Guid.Parse(token.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        var disabled = new SessionQueries(db, Options.Create(new SecurityConfiguration { Accounts = new() { Subaccounts = false } }));
        Assert.True(await disabled.TryFindById(switchedSession) == None);
        Assert.True(await disabled.TryFindById(direct) != None);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "email");
        Assert.True((await queries.TryFindById(parentSession)).Match(() => false, value => value is InvalidSession));
        var targets = (await List(switchedSession)).GetProperty("Accounts").EnumerateArray().Select(item => item.GetProperty("UserId").GetGuid()).ToArray();
        Assert.Contains(secondId, targets);
        Assert.Equal(primaryPin is not null, targets.Contains(primary));
        var outsider = await DatabaseSeedHelper.SeedHumanUserAsync(db, "outsider@example.test");
        Assert.IsType<NotFound>(await Switch(switchedSession, outsider, primaryPin));
        Assert.IsType<ForbidHttpResult>(await Switch(switchedSession, primary));
        Assert.IsType<ForbidHttpResult>(await Switch(switchedSession, primary, "000111"));
        var sibling = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(switchedSession, secondId));
        var siblingToken = new JwtSecurityTokenHandler().ReadJwtToken(sibling.Value!.AccessToken);
        var siblingSession = Guid.Parse(siblingToken.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        Assert.True((await queries.TryFindByIdWithUserId(siblingSession)).Match(() => false,
            value => value.UserId == secondId && value.Session is ValidSession { FamilyPrimaryUserId: var familyId } && familyId == primary));
        if (primaryPin is not null)
        {
            var returned = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(siblingSession, primary, primaryPin));
            var returnedToken = new JwtSecurityTokenHandler().ReadJwtToken(returned.Value!.AccessToken);
            var returnedSession = Guid.Parse(returnedToken.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
            Assert.True((await List(returnedSession)).GetProperty("IsPrimary").GetBoolean());
            Assert.False((await List(returnedSession)).GetProperty("LinkUnlinkEnabled").GetBoolean());
            var directParent = await Start(primary);
            var release = await SubaccountLinkEndpoints.Release(firstId, SessionTestHelper.SignedIn(directParent), queries, db,
                TimeProvider.System, Options.Create(security.Value with { EmailConfirmation = false }));
            Assert.IsType<NoContent>(release);
            Assert.IsType<ForbidHttpResult>(await Switch(returnedSession, secondId));
            Assert.True((await List(directParent)).GetProperty("IsPrimary").GetBoolean());
            Assert.True((await queries.TryFindById(siblingSession)).Match(() => false, value => value is InvalidSession));
        }
    }
}

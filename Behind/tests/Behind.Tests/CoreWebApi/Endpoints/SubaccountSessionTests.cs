using System.IdentityModel.Tokens.Jwt;
using Behind.Core.Common.Configuration;
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
    [Fact]
    public async Task OnlyASwitchedSessionCanMoveBetweenSiblingsAndNeverIntoThePrimary()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var primaryUser = await db.Users.FindAsync(primary) ?? throw new InvalidOperationException();
        var first = new HumanUser { DisplayName = "First" };
        var second = new HumanUser { DisplayName = "Second" };
        db.Users.AddRange(first, second);
        var firstId = db.GetKey(first);
        var secondId = db.GetKey(second);
        db.Subaccounts.AddRange(new Subaccount(primaryUser, first, true, null), new Subaccount(primaryUser, second, true, null));
        await db.SaveChangesAsync();
        var commands = new SessionCommands(db);
        var queries = new SessionQueries(db);
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(firstId.ToString()).Returns(new ApplicationUser { Id = firstId, DisplayName = "First" });
        manager.FindByIdAsync(secondId.ToString()).Returns(new ApplicationUser { Id = secondId, DisplayName = "Second" });
        var jwt = new JwtSettings("issuer", "audience", new string('k', 64));
        var security = Options.Create(new SecurityConfiguration(false, None, Some(jwt), None));
        Task<Guid> Start(Guid userId) => commands.CreateForUserId(userId,
            new ValidSession(AccessToken.Create(first), RefreshToken.Create(first), "127.0.0.1", "test"));
        Task<IResult> Switch(Guid sessionId, Guid target)
        {
            var http = SessionTestHelper.SignedIn(sessionId);
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
            http.Request.Headers.UserAgent = "test";
            return SubaccountEndpoints.Switch(target, new(null), http, queries, db, manager, security, commands,
                new WallOfShameQueries(db), TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);
        }

        var direct = await Start(firstId);
        Assert.IsType<ForbidHttpResult>(await Switch(direct, secondId));

        var parentSession = await Start(primary);
        var entered = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(parentSession, firstId));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(entered.Value!.AccessToken);
        var switchedSession = Guid.Parse(token.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "email");
        Assert.True((await queries.TryFindById(parentSession)).Match(() => false, value => value is InvalidSession));
        Assert.IsType<NotFound>(await Switch(switchedSession, primary));
        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(switchedSession, secondId));
    }
}

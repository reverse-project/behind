using System.Security.Claims;
using Behind.Common.Database.Modules;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
        var request = new SubaccountEndpoints.CreateRequest("Child", email, birthday, true, pin);
        await SubaccountEndpoints.Create(request, parent.Http, parent.Queries, db, roles, TimeProvider.System);
        var link = await db.Subaccounts.SingleAsync();
        Assert.Equal(primary, link.PrimaryUserId);
        Assert.NotEqual(primary, link.UserId);
        Assert.Equal(pin is not null, link.PinHash is not null);
        if (pin is not null)
        {
            Assert.NotEqual(pin, link.PinHash);
            Assert.True(PasswordHash.CreateFromExisting(link.PinHash!).Verify(pin));
        }
        var profiles = new UserProfiles(db, Substitute.For<IModuleFiles>());
        await profiles.SetDetails(link.UserId, new(new DateOnly(1980, 1, 1), false), default);
        Assert.Equal(new UserProfiles.Details(birthday, false, true), await profiles.GetDetails(link.UserId, default));
        await SubaccountEndpoints.List(parent.Http, parent.Queries, db);

        var child = AsUser(link.UserId);
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(request, child.Http, child.Queries, db, roles, TimeProvider.System));
        var other = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db, "elsewhere@example.test"));
        var update = new SubaccountEndpoints.UpdateRequest("Different", birthday, false, null, true);
        Assert.IsType<NotFound>(await SubaccountEndpoints.Update(link.UserId, update, other.Http, other.Queries, db, TimeProvider.System));

        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(link.UserId.ToString()).Returns(new ApplicationUser { Id = link.UserId });
        var security = Options.Create(new SecurityConfiguration(false, None, Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var commands = Substitute.For<ISessionCommands>();
        Task<IResult> Switch(string? suppliedPin) => SubaccountEndpoints.Switch(link.UserId, new(suppliedPin),
            parent.Http, parent.Queries, db, manager, security, commands, Substitute.For<IWallOfShameQueries>(),
            TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);

        if (pin is not null)
        {
            Assert.IsType<ForbidHttpResult>(await Switch("000000"));
            await commands.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
        }
        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(pin));
        await commands.Received(1).CreateForUserId(link.UserId, Arg.Is<ValidSession>(s => s.FamilyPrimaryUserId == primary), Arg.Any<CancellationToken>());
        await commands.Received(1).InvalidateSession(parent.SessionId, primary, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        Assert.IsType<NoContent>(await SubaccountEndpoints.Update(link.UserId, update, parent.Http, parent.Queries, db, TimeProvider.System));
        Assert.Null((await db.Subaccounts.AsNoTracking().SingleAsync()).PinHash);
    }
}

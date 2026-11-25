using System.Security.Claims;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.MagicLinks;

public sealed class PinMagicProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmailedProofIsOneTimeScopedAndExpires(bool expire)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var parent = new HumanUser("Parent", [new Email("parent@example.test", now)], None);
        var child = new HumanUser { DisplayName = "Child" };
        db.Subaccounts.Add(new Subaccount(parent, child, true));
        db.UserSwitchPins.Add(new UserSwitchPin(child, new PinHash(PasswordHash.Create("0007"))));
        await db.SaveChangesAsync();
        var actorId = db.GetKey(parent);
        var targetId = db.GetKey(child);
        var sessionId = Guid.NewGuid();
        var sessions = Substitute.For<ISessionQueries>();
        sessions.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((new ValidSession(AccessToken.Create(parent),
                RefreshToken.Create(parent), "127.0.0.1", "test"), actorId)));
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(actorId.ToString()).Returns(new ApplicationUser { Id = actorId, Emails = parent.Emails });
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Frontend:Url"] = "https://example.test/" }).Build();
        var store = new MagicLinkSettingsStore(Some(new MagicLinkSettings(Uuid.CreateVersion7(now), None,
            LinkLifetime: Some(TimeSpan.FromMinutes(5)))));
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var commands = new MagicLinkCommands(db);
        var queue = new MagicLinkMailQueue();
        using var services = new ServiceCollection().AddOptions()
            .AddSingleton<IMagicLinkSettingsStore>(store).AddSingleton(security)
            .AddSingleton<IMagicLinkCommands>(commands)
            .AddSingleton<TimeProvider>(new FixedTimeProvider(expire ? now.AddMinutes(5) : now))
            .BuildServiceProvider();
        var http = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", sessionId.ToString())], "test"))
        };
        Task<IResult> Issue(Guid target) => PinMagicProof.Request(target, new("remove"), http, sessions, db, manager,
            config, store, security, commands, new MagicLinkQueries(db), queue, new FixedTimeProvider(now));
        Assert.IsType<BadRequest>(await PinMagicProof.Request(targetId, new("unknown"), http, sessions, db, manager,
            config, store, security, commands, new MagicLinkQueries(db), queue, new FixedTimeProvider(now)));
        Assert.IsType<ForbidHttpResult>(await Issue(Guid.NewGuid()));
        Assert.False(queue.Reader.TryRead(out _));
        Assert.IsType<NoContent>(await Issue(targetId));
        Assert.True(queue.Reader.TryRead(out var mail));
        Assert.Equal("parent@example.test", mail.Email);
        Assert.Equal(MagicLinkFunction.SwitchPin, mail.Function);
        var url = new Uri(mail.Link);
        Assert.Empty(url.Query);
        Assert.StartsWith("#/profile/pin-proof?", url.Fragment);
        var query = QueryHelpers.ParseQuery(url.Fragment.Split('?')[1]);
        var token = query["pinProof"].ToString();
        Assert.False(await PinMagicProof.Consume(token, actorId, targetId, PinProofOperation.Change, http));
        Assert.False(await PinMagicProof.Consume(token, actorId, actorId, PinProofOperation.Remove, http));
        Assert.False(await PinMagicProof.Consume(token, targetId, targetId, PinProofOperation.Remove, http));
        var remove = new SubaccountEndpoints.PinRequest(null, true, new(MagicToken: token));
        var result = await SubaccountEndpoints.ChangePin(targetId, remove, http, sessions, db);
        Assert.IsType(expire ? typeof(ForbidHttpResult) : typeof(NoContent), result);
        Assert.Equal(expire, await db.UserSwitchPins.AnyAsync());
        Assert.False(await PinMagicProof.Consume(token, actorId, targetId, PinProofOperation.Remove, http));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.Middleware;
using Behind.Core.WebApi.TwoFactor;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountSignInNotificationTests
{
    [Fact]
    public async Task SqliteNotifiesOnlyForCommittedLinkedSubaccountSessions()
    {
        using var factory = new TestDbContextFactory();
        await VerifyNotifications(factory.CreateContext);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresNotifiesOnlyForCommittedLinkedSubaccountSessions()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        await using var postgres = new PostgreSqlBuilder(configuration["Postgres:Image"]
            ?? throw new InvalidOperationException("Postgres:Image is required.")).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var setup = new CoreContextPostgres(options);
        await setup.Database.MigrateAsync();
        await VerifyNotifications(() => new CoreContextPostgres(options));
    }

    private static async Task VerifyNotifications(Func<AbstractCoreContext> contextFactory)
    {
        var now = TimeProvider.System.GetUtcNow();
        var password = PasswordHash.Create("test-password");
        var primary = new HumanUser
        {
            Emails = [new Email("primary@example.test", now), new Email("unconfirmed@example.test")]
        };
        var child = new HumanUser
        {
            DisplayName = "<Child>",
            Emails = [new Email("child@example.test", now)]
        };
        var emailLess = new HumanUser { DisplayName = "No email" };
        var outsider = new HumanUser { Emails = [new Email("outsider@example.test", now)] };
        await using var db = contextFactory();
        db.Users.AddRange(primary, child, emailLess, outsider);
        db.Subaccounts.AddRange(new Subaccount(primary, child, true), new Subaccount(primary, emailLess, true));
        await db.SaveChangesAsync();
        var primaryId = db.GetKey(primary);
        var childId = db.GetKey(child);
        var emailLessId = db.GetKey(emailLess);

        var users = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        var primaryUser = primary.ToApplicationUser(primaryId);
        primaryUser.Password = password;
        var childUser = child.ToApplicationUser(childId);
        childUser.Password = password;
        var outsiderUser = outsider.ToApplicationUser(db.GetKey(outsider));
        outsiderUser.Password = password;
        users.FindByEmailAsync("outsider@example.test").Returns(outsiderUser);
        users.FindByEmailAsync("primary@example.test").Returns(primaryUser);
        users.FindByEmailAsync("child@example.test").Returns(childUser);
        users.FindByIdAsync(emailLessId.ToString()).Returns(emailLess.ToApplicationUser(emailLessId));

        var mail = Substitute.For<IEmailSender>();
        await using var services = new ServiceCollection()
            .AddScoped<AbstractCoreContext>(_ => contextFactory())
            .AddScoped<IEmailQueries, EmailQueries>()
            .AddSingleton(mail)
            .BuildServiceProvider();
        var security = Options.Create(new SecurityConfiguration(
            false, None, Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var commands = new SessionCommands(db);
        var queries = new SessionQueries(db);
        var bans = new WallOfShameQueries(db);
        var gate = Substitute.For<ITwoFactorGate>();

        Task<IResult> SignIn(HttpContext http, string email, string credential) =>
            SignInEndpoints.SignInPassword(users, security, commands, bans, gate, TimeProvider.System,
                http, NullLogger<SignInEndpoints>.Instance, new(email, credential));

        DefaultHttpContext WithServices(DefaultHttpContext http)
        {
            http.RequestServices = services;
            http.Connection.RemoteIpAddress = IPAddress.Loopback;
            http.Request.Headers.UserAgent = "test";
            return http;
        }

        var direct = WithServices(new DefaultHttpContext());
        Assert.IsType<UnauthorizedHttpResult>(await SignIn(direct, "child@example.test", "wrong"));
        gate.Challenge(childId, null, Arg.Any<CancellationToken>())
            .Returns(Some(new TwoFactorChallengeResponse("ticket", [TwoFactorMethod.Totp], false)));
        Assert.IsType<JsonHttpResult<TwoFactorChallengeResponse>>(await SignIn(direct, "child@example.test", "test-password"));
        gate.Challenge(childId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<TwoFactorChallengeResponse>>(None));

        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
            await SignIn(WithServices(new DefaultHttpContext()), "outsider@example.test", "test-password"));
        var parentSignIn = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
            await SignIn(WithServices(new DefaultHttpContext()), "primary@example.test", "test-password"));
        await mail.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);

        SignInEndpoints.SignInResponse signedIn;
        await using (var transaction = new AuthenticationTransaction(db))
        {
            direct.Features.Set(transaction);
            await transaction.BeginAsync();
            signedIn = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
                await SignIn(direct, "child@example.test", "test-password")).Value!;
            await mail.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);
            await transaction.CommitAsync();
        }
        await mail.Received(1).SendEmailAsync("primary@example.test", "Signed in to a linked subaccount",
            Arg.Is<string>(html => html.Contains("&lt;Child&gt;") && !html.Contains("child@example.test")));
        Assert.Single(mail.ReceivedCalls());
        mail.ClearReceivedCalls();

        var refresh = WithServices(new DefaultHttpContext());
        refresh.Request.Headers[SessionEndpoints.RefreshTokenHeader] = signedIn.RefreshToken;
        var roles = Substitute.For<IAccessQueries>();
        roles.GetUserAccess(childId, Arg.Any<CancellationToken>()).Returns(new UserAccess([], [], []));
        var renewed = Assert.IsType<JsonHttpResult<SessionResponse>>(await SessionEndpoints.Get(
            refresh, security, queries, commands, roles, TimeProvider.System, NullLogger<SessionEndpoints>.Instance));
        Assert.NotNull(renewed.Value?.Refreshed);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(parentSignIn.Value!.AccessToken);
        var parentSessionId = Guid.Parse(token.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        var switchContext = WithServices(SessionTestHelper.SignedIn(parentSessionId));
        Assert.IsType<NotFound>(await SubaccountEndpoints.Switch(outsiderUser.Id, new(null), switchContext,
            queries, db, users, security, commands, bans, TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance));
        await using (var transaction = new AuthenticationTransaction(db))
        {
            switchContext.Features.Set(transaction);
            await transaction.BeginAsync();
            Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch());
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        await mail.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);

        await using (var transaction = new AuthenticationTransaction(db))
        {
            switchContext.Features.Set(transaction);
            await transaction.BeginAsync();
            var switched = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch());
            await transaction.CommitAsync();
            var switchedToken = new JwtSecurityTokenHandler().ReadJwtToken(switched.Value!.AccessToken);
            var switchedSessionId = Guid.Parse(switchedToken.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
            Assert.True((await queries.TryFindById(switchedSessionId)).Match(() => false, value => value is ValidSession));
        }
        await mail.Received(1).SendEmailAsync("primary@example.test", "Switched into a linked subaccount",
            Arg.Is<string>(html => html.Contains("No email")));
        Assert.Single(mail.ReceivedCalls());
        mail.ClearReceivedCalls();

        mail.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("test delivery failure")));
        await using (var transaction = new AuthenticationTransaction(db))
        {
            direct.Features.Set(transaction);
            await transaction.BeginAsync();
            Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
                await SignIn(direct, "child@example.test", "test-password"));
            await transaction.CommitAsync();
        }
        Assert.Single(mail.ReceivedCalls());

        Task<IResult> Switch() => SubaccountEndpoints.Switch(emailLessId, new(null), switchContext,
            queries, db, users, security, commands, bans, TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);
    }
}

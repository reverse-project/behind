using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Core.WebApi.EmailSender;
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

public sealed class EmailSignInNotificationTests
{
    [Fact]
    public async Task SqliteNotifiesOnlyForCommittedVerifiedEmailSignIns()
    {
        using var factory = new TestDbContextFactory();
        await VerifyNotifications(factory.CreateContext);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresNotifiesOnlyForCommittedVerifiedEmailSignIns()
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
        const string childPin = "0042";
        var password = PasswordHash.Create("test-password");
        var primary = new HumanUser
        {
            Emails =
            [
                new Email("primary@example.test", now),
                new Email("another@example.test", now),
                new Email("unconfirmed@example.test")
            ]
        };
        var child = new HumanUser
        {
            DisplayName = "<Child>",
            Emails = [new Email("child@example.test", now)]
        };
        var outsider = new HumanUser { Emails = [new Email("outsider@example.test", now)] };
        await using var db = contextFactory();
        db.Users.AddRange(primary, child, outsider);
        db.Subaccounts.Add(new Subaccount(primary, child, true));
        db.UserSwitchPins.Add(new UserSwitchPin(child, new PinHash(PasswordHash.Create(childPin))));
        await db.SaveChangesAsync();
        var primaryId = db.GetKey(primary);
        var childId = db.GetKey(child);

        var users = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        var primaryUser = primary.ToApplicationUser(primaryId);
        primaryUser.Password = password;
        var childUser = child.ToApplicationUser(childId);
        childUser.Password = password;
        var outsiderId = db.GetKey(outsider);
        users.FindByEmailAsync("ANOTHER@example.test").Returns(primaryUser);
        users.FindByEmailAsync("unconfirmed@example.test").Returns(primaryUser);
        users.FindByEmailAsync("child@example.test").Returns(childUser);
        users.FindByIdAsync(childId.ToString()).Returns(childUser);

        var mail = Substitute.For<IEmailSender>();
        await using var services = new ServiceCollection()
            .AddSingleton(mail)
            .BuildServiceProvider();
        var settings = new SecurityConfiguration(
            false, None, Some(new JwtSettings("issuer", "audience", new string('k', 64))), None);
        var security = Options.Create(settings);
        var commands = new SessionCommands(db);
        var queries = new SessionQueries(db);
        var bans = new WallOfShameQueries(db);
        var gate = Substitute.For<ITwoFactorGate>();

        Task<IResult> SignIn(HttpContext http, string email, string credential, bool confirmationRequired = true) =>
            SignInEndpoints.SignInPassword(users, Options.Create(settings with { EmailConfirmation = confirmationRequired }),
                commands, bans, gate, TimeProvider.System, http, NullLogger<SignInEndpoints>.Instance, new(email, credential));

        DefaultHttpContext WithServices(DefaultHttpContext http)
        {
            http.RequestServices = services;
            http.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.24");
            http.Request.Headers["X-Forwarded-For"] = "203.0.113.10";
            http.Request.Headers.UserAgent = "test <script>&";
            return http;
        }

        var direct = WithServices(new DefaultHttpContext());
        Assert.IsType<UnauthorizedHttpResult>(await SignIn(direct, "child@example.test", "wrong"));
        gate.ChallengeEmail(childId, null, Arg.Any<EmailSignInContext>(), Arg.Any<CancellationToken>())
            .Returns(Some(new TwoFactorChallengeResponse("ticket", [TwoFactorMethod.Totp], false)));
        Assert.IsType<JsonHttpResult<TwoFactorChallengeResponse>>(await SignIn(direct, "child@example.test", "test-password"));
        gate.ChallengeEmail(childId, null, Arg.Any<EmailSignInContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<TwoFactorChallengeResponse>>(None));

        Assert.IsType<JsonHttpResult<SignInEndpoints.EmailNotConfirmedResponse>>(
            await SignIn(direct, "unconfirmed@example.test", "test-password"));
        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
            await SignIn(direct, "unconfirmed@example.test", "test-password", confirmationRequired: false));

        await using (var transaction = new AuthenticationTransaction(db))
        {
            direct.Features.Set(transaction);
            await transaction.BeginAsync();
            Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
                await SignIn(direct, "child@example.test", "test-password"));
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        await mail.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);

        var parentSignIn = await CommitSignIn("ANOTHER@example.test");
        await mail.Received(1).SendEmailAsync("another@example.test", "New sign-in to your account",
            Arg.Is<string>(html => html.Contains("198.51.100.24") && !html.Contains("203.0.113.10")
                && html.Contains("test &lt;script&gt;&amp;") && !html.Contains("<script>")));
        Assert.Single(mail.ReceivedCalls());
        mail.ClearReceivedCalls();

        var signedIn = await CommitSignIn("child@example.test");
        await mail.Received(1).SendEmailAsync("child@example.test", "New sign-in to your account",
            Arg.Is<string>(html => html.Contains("198.51.100.24") && html.Contains("test &lt;script&gt;&amp;")));
        Assert.Single(mail.ReceivedCalls());
        mail.ClearReceivedCalls();

        var refresh = WithServices(new DefaultHttpContext());
        refresh.Request.Headers[SessionEndpoints.RefreshTokenHeader] = signedIn.RefreshToken;
        var roles = Substitute.For<IAccessQueries>();
        roles.GetUserAccess(childId, Arg.Any<CancellationToken>()).Returns(new UserAccess([], [], []));
        var renewed = Assert.IsType<JsonHttpResult<SessionResponse>>(await SessionEndpoints.Get(
            refresh, security, queries, commands, roles, TimeProvider.System, NullLogger<SessionEndpoints>.Instance));
        Assert.NotNull(renewed.Value?.Refreshed);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(parentSignIn.AccessToken);
        var parentSessionId = Guid.Parse(token.Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        var switchContext = WithServices(SessionTestHelper.SignedIn(parentSessionId));
        Assert.IsType<NotFound>(await SubaccountEndpoints.Switch(outsiderId, new(null), switchContext,
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
        await mail.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);

        mail.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("test delivery failure")));
        var signedInDespiteDeliveryFailure = await CommitSignIn("child@example.test");
        var sessionId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(signedInDespiteDeliveryFailure.AccessToken)
            .Claims.Single(claim => claim.Type == SessionClaimTypes.SessionId).Value);
        Assert.True((await queries.TryFindById(sessionId)).Match(() => false, value => value is ValidSession));
        Assert.Single(mail.ReceivedCalls());

        async Task<SignInEndpoints.SignInResponse> CommitSignIn(string address)
        {
            var http = WithServices(new DefaultHttpContext());
            await using var transaction = new AuthenticationTransaction(db);
            http.Features.Set(transaction);
            await transaction.BeginAsync();
            var response = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
                await SignIn(http, address, "test-password"));
            await mail.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);
            await transaction.CommitAsync();
            return response.Value!;
        }

        Task<IResult> Switch() => SubaccountEndpoints.Switch(childId, new(childPin), switchContext,
            queries, db, users, security, commands, bans, TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);
    }
}

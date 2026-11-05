using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Behind.Common;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SignInEndpointsSessionTests
{
    private static readonly JwtSettings Jwt = new("issuer", "audience", new string('k', 64));
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static IWallOfShameQueries NoBans() => Substitute.For<IWallOfShameQueries>();

    private static ITwoFactorGate NoTwoFactor() => Substitute.For<ITwoFactorGate>();

    private static IWallOfShameQueries BannedFor(string reason)
    {
        var queries = Substitute.For<IWallOfShameQueries>();
        queries.FindActiveBan(Arg.Any<Guid>(), Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IWallOfShameQueries.ActiveBan(reason, None)));
        return queries;
    }

    private static IOptions<SecurityConfiguration> Security(
        Option<JwtSettings> jwt,
        bool emailConfirmation = true) =>
        Options.Create(new SecurityConfiguration(false, None, jwt, None, EmailConfirmation: emailConfirmation));

    private static UserManager<ApplicationUser> UserManagerFor(ApplicationUser? user)
    {
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByEmailAsync(Arg.Any<string>()).Returns(user);
        return manager;
    }

    private static ApplicationUser NewUser(string password, bool emailConfirmed = true) =>
        new()
        {
            DisplayName = "User",
            Emails = [new Email("user@test.com", emailConfirmed ? Now : null)],
            Password = PasswordHash.Create(password)
        };

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
        context.Request.Headers.UserAgent = "unit-test";
        return context;
    }

    [Fact]
    public async Task SignInPassword_ValidCredentials_PersistsSessionAndBindsTokens()
    {
        var user = NewUser("secret");
        var sessionId = Guid.NewGuid();
        var commands = Substitute.For<ISessionCommands>();
        commands.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(sessionId);

        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(user),
            Security(Some(Jwt)),
            commands,
            NoBans(),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        var json = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result);
        var persisted = Assert.IsType<ValidSession>(
            commands.ReceivedCalls().Single().GetArguments()[1]);
        await commands.Received(1).CreateForUserId(user.Id, persisted, Arg.Any<CancellationToken>());

        Assert.Equal("10.1.2.3", persisted.RemoteAddress.Value);
        Assert.Equal("unit-test", persisted.UserAgent.Value);

        var access = new JwtSecurityTokenHandler().ReadJwtToken(json.Value!.AccessToken);
        var refresh = new JwtSecurityTokenHandler().ReadJwtToken(json.Value.RefreshToken);
        Assert.Equal(sessionId.ToString(), access.Claims.Single(c => c.Type == SessionClaimTypes.SessionId).Value);
        Assert.Equal(sessionId.ToString(), refresh.Claims.Single(c => c.Type == SessionClaimTypes.SessionId).Value);
        Assert.Equal(
            persisted.AccessToken.Nonce.ToString(),
            access.Claims.Single(c => c.Type == SessionClaimTypes.TokenNonce).Value);
        Assert.Equal(
            persisted.RefreshToken.Nonce.ToString(),
            refresh.Claims.Single(c => c.Type == SessionClaimTypes.TokenNonce).Value);
    }

    [Fact]
    public async Task SignInPassword_WrongPassword_ReturnsUnauthorizedAndPersistsNothing()
    {
        var commands = Substitute.For<ISessionCommands>();

        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret")),
            Security(Some(Jwt)),
            commands,
            NoBans(),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "wrong"));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs()
            .CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task SignInPassword_UnknownUser_ReturnsUnauthorized()
    {
        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(null),
            Security(Some(Jwt)),
            Substitute.For<ISessionCommands>(),
            NoBans(),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task SignInPassword_WithoutJwtSettings_ReturnsServerError()
    {
        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret")),
            Security(None),
            Substitute.For<ISessionCommands>(),
            NoBans(),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        Assert.IsType<InternalServerError>(result);
    }

    [Fact]
    public async Task SignInPassword_UnconfirmedEmailWhenConfirmationRequired_ReturnsForbiddenAndStartsNoSession()
    {
        var commands = Substitute.For<ISessionCommands>();

        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret", emailConfirmed: false)),
            Security(Some(Jwt)),
            commands,
            NoBans(),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        var json = Assert.IsType<JsonHttpResult<SignInEndpoints.EmailNotConfirmedResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
        Assert.Equal(SignInEndpoints.EmailNotConfirmedCode, json.Value!.Code);
        await commands.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task SignInPassword_UnconfirmedEmailWhenConfirmationNotRequired_StartsSession()
    {
        var commands = Substitute.For<ISessionCommands>();
        commands.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret", emailConfirmed: false)),
            Security(Some(Jwt), emailConfirmation: false),
            commands,
            NoBans(),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result);
    }

    private static readonly TwoFactorChallengeResponse Challenge =
        new("ticket", [TwoFactorMethod.Totp, TwoFactorMethod.Recovery], false);

    private static ITwoFactorGate ChallengingGate()
    {
        var gate = Substitute.For<ITwoFactorGate>();
        gate.Challenge(Arg.Any<Guid>(), Arg.Any<MagicLinkFunction?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Some(Challenge)));

        return gate;
    }

    private static Task<IResult> SignInWithGate(
        ApplicationUser? user,
        string password,
        ITwoFactorGate gate,
        ISessionCommands commands,
        HttpContext? httpContext = null,
        IWallOfShameQueries? bans = null) =>
        SignInEndpoints.SignInPassword(
            UserManagerFor(user),
            Security(Some(Jwt)),
            commands,
            bans ?? NoBans(),
            gate,
            new FixedTimeProvider(Now),
            httpContext ?? NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", password));

    [Fact]
    public async Task SignInPassword_WhenASecondFactorIsRequired_ReturnsTheChallengeInsteadOfASession()
    {
        var commands = Substitute.For<ISessionCommands>();
        var httpContext = NewHttpContext();

        var result = await SignInWithGate(NewUser("secret"), "secret", ChallengingGate(), commands, httpContext);

        var json = Assert.IsType<JsonHttpResult<TwoFactorChallengeResponse>>(result);
        Assert.Equal(Challenge, json.Value);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());
        await commands.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task SignInPassword_AsksTheGateAboutTheUserOfTheCredentials()
    {
        var user = NewUser("secret");
        var gate = ChallengingGate();

        await SignInWithGate(user, "secret", gate, Substitute.For<ISessionCommands>());

        await gate.Received(1).Challenge(user.Id, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignInPassword_WrongPassword_DoesNotAskTheGate()
    {
        var gate = ChallengingGate();

        var result = await SignInWithGate(NewUser("secret"), "wrong", gate, Substitute.For<ISessionCommands>());

        Assert.IsType<UnauthorizedHttpResult>(result);
        await gate.DidNotReceiveWithAnyArgs().Challenge(default, default, default);
    }

    [Fact]
    public async Task SignInPassword_UnconfirmedEmail_DoesNotAskTheGate()
    {
        var gate = ChallengingGate();

        var result = await SignInWithGate(
            NewUser("secret", emailConfirmed: false), "secret", gate, Substitute.For<ISessionCommands>());

        Assert.IsType<JsonHttpResult<SignInEndpoints.EmailNotConfirmedResponse>>(result);
        await gate.DidNotReceiveWithAnyArgs().Challenge(default, default, default);
    }

    [Fact]
    public async Task SignInPassword_BannedUser_DoesNotAskTheGate()
    {
        var gate = ChallengingGate();

        var result = await SignInWithGate(
            NewUser("secret"), "secret", gate, Substitute.For<ISessionCommands>(), bans: BannedFor("spam"));

        Assert.IsType<JsonHttpResult<SignInEndpoints.BannedResponse>>(result);
        await gate.DidNotReceiveWithAnyArgs().Challenge(default, default, default);
    }

    [Fact]
    public async Task SignInPassword_BannedUser_ReturnsForbiddenWithTheBanReasonAndStartsNoSession()
    {
        var commands = Substitute.For<ISessionCommands>();

        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret")),
            Security(Some(Jwt)),
            commands,
            BannedFor("spamming the wall"),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "secret"));

        var json = Assert.IsType<JsonHttpResult<SignInEndpoints.BannedResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
        Assert.Equal("spamming the wall", json.Value!.Reason);
        await commands.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task SignInPassword_BannedUserWithWrongPassword_DoesNotRevealTheBan()
    {
        var result = await SignInEndpoints.SignInPassword(
            UserManagerFor(NewUser("secret")),
            Security(Some(Jwt)),
            Substitute.For<ISessionCommands>(),
            BannedFor("spamming the wall"),
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            NullLogger<SignInEndpoints>.Instance,
            new SignInEndpoints.PasswordRequest("user@test.com", "wrong"));

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    private async Task<CapturingLogger<SignInEndpoints>> SignInWithLoggingAsync(
        ApplicationUser? user,
        string password,
        IWallOfShameQueries bans,
        ISessionCommands commands,
        Option<JwtSettings>? jwt = null)
    {
        var logger = new CapturingLogger<SignInEndpoints>();

        await SignInEndpoints.SignInPassword(
            UserManagerFor(user),
            Security(jwt ?? Some(Jwt)),
            commands,
            bans,
            NoTwoFactor(),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            logger,
            new SignInEndpoints.PasswordRequest("user@test.com", password));

        return logger;
    }

    [Fact]
    public async Task SignInPassword_WrongPassword_LogsWarningWithUserAndReason()
    {
        var user = NewUser("secret");

        var logger = await SignInWithLoggingAsync(user, "wrong", NoBans(), Substitute.For<ISessionCommands>());

        var entry = logger.Single(LogLevel.Warning);
        Assert.Equal(user.Id, entry.State["UserId"]);
        Assert.Equal("wrong password", entry.State["Reason"]);
    }

    [Fact]
    public async Task SignInPassword_UnknownUser_LogsWarningWithoutUserId()
    {
        var logger = await SignInWithLoggingAsync(null, "secret", NoBans(), Substitute.For<ISessionCommands>());

        var entry = logger.Single(LogLevel.Warning);
        Assert.Null(entry.State["UserId"]);
        Assert.Equal("unknown email", entry.State["Reason"]);
    }

    [Fact]
    public async Task SignInPassword_ValidCredentials_LogsInformationWithSessionAndUser()
    {
        var user = NewUser("secret");
        var sessionId = Guid.NewGuid();
        var commands = Substitute.For<ISessionCommands>();
        commands.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(sessionId);

        var logger = await SignInWithLoggingAsync(user, "secret", NoBans(), commands);

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal(sessionId, entry.State["SessionId"]);
        Assert.Equal(user.Id, entry.State["UserId"]);
        Assert.False(logger.Has(LogLevel.Warning));
    }

    [Fact]
    public async Task SignInPassword_BannedUser_LogsWarningWithUser()
    {
        var user = NewUser("secret");

        var logger = await SignInWithLoggingAsync(
            user,
            "secret",
            BannedFor("spamming the wall"),
            Substitute.For<ISessionCommands>());

        Assert.Equal(user.Id, logger.Single(LogLevel.Warning).State["UserId"]);
        Assert.False(logger.Has(LogLevel.Information));
    }

    [Fact]
    public async Task SignInPassword_WithoutJwtSettings_LogsErrorWithOperation()
    {
        var logger = await SignInWithLoggingAsync(
            NewUser("secret"),
            "secret",
            NoBans(),
            Substitute.For<ISessionCommands>(),
            None);

        Assert.Equal(nameof(SignInEndpoints.SignInPassword), logger.Single(LogLevel.Error).State["Operation"]);
    }
}

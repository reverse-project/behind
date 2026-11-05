using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class TwoFactorSignInServiceTests
{
    private static readonly JwtSettings Jwt = new("issuer", "audience", new string('k', 64));
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly ISessionCommands _sessions = Substitute.For<ISessionCommands>();
    private readonly IWallOfShameQueries _bans = Substitute.For<IWallOfShameQueries>();
    private readonly UserManager<ApplicationUser> _userManager = Substitute.For<UserManager<ApplicationUser>>(
        Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
    private readonly ApplicationUser _user = new()
    {
        DisplayName = "User",
        Emails = [new Email("user@test.com", Now)]
    };

    private TwoFactorSignIn Service(Option<JwtSettings>? jwt = null)
    {
        _userManager.FindByIdAsync(_user.Id.ToString()).Returns(_user);

        return new TwoFactorSignIn(
            _userManager,
            Options.Create(new SecurityConfiguration(false, None, jwt ?? Some(Jwt), None)),
            _sessions,
            _bans,
            new FixedTimeProvider(Now),
            NullLogger<TwoFactorSignIn>.Instance);
    }

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
        context.Request.Headers.UserAgent = "unit-test";

        return context;
    }

    [Fact]
    public async Task Complete_StartsASessionForTheUserOfTheTicket()
    {
        var sessionId = Guid.NewGuid();
        _sessions.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(sessionId);

        var result = await Service().Complete(_user.Id, MagicLinkFunction.LostPassword, null, NewHttpContext());

        var json = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result);
        var access = new JwtSecurityTokenHandler().ReadJwtToken(json.Value!.AccessToken);
        Assert.Equal(sessionId.ToString(), access.Claims.Single(c => c.Type == SessionClaimTypes.SessionId).Value);
        Assert.Equal(MagicLinkFunction.LostPassword, json.Value.Function);
        Assert.Null(json.Value.RecoveryCodes);
        await _sessions.Received(1).CreateForUserId(_user.Id, Arg.Any<ValidSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_HandsOverTheNewRecoveryCodes()
    {
        _sessions.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var result = await Service().Complete(_user.Id, null, ["AAAAAAAA", "BBBBBBBB"], NewHttpContext());

        Assert.Equal(
            ["AAAAAAAA", "BBBBBBBB"],
            Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result).Value!.RecoveryCodes);
    }

    [Fact]
    public async Task Complete_NoRecoveryCodesAreNotReported()
    {
        _sessions.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var result = await Service().Complete(_user.Id, null, [], NewHttpContext());

        Assert.Null(Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result).Value!.RecoveryCodes);
    }

    [Fact]
    public async Task Complete_UnknownUserIsUnauthorized()
    {
        var result = await Service().Complete(Guid.NewGuid(), null, null, NewHttpContext());

        Assert.IsType<UnauthorizedHttpResult>(result);
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task Complete_BannedUserGetsNoSession()
    {
        _bans.FindActiveBan(_user.Id, Now, Arg.Any<CancellationToken>())
            .Returns(Some(new IWallOfShameQueries.ActiveBan("spam", None)));

        var result = await Service().Complete(_user.Id, null, null, NewHttpContext());

        Assert.IsType<JsonHttpResult<SignInEndpoints.BannedResponse>>(result);
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task Complete_UnconfirmedEmailGetsNoSessionWhenConfirmationIsRequired()
    {
        var unconfirmed = new ApplicationUser { DisplayName = "User", Emails = [new Email("user@test.com")] };
        _userManager.FindByIdAsync(unconfirmed.Id.ToString()).Returns(unconfirmed);

        var result = await Service().Complete(unconfirmed.Id, null, null, NewHttpContext());

        Assert.IsType<JsonHttpResult<SignInEndpoints.EmailNotConfirmedResponse>>(result);
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task Complete_WithoutJwtSettingsIsAServerError()
    {
        var result = await Service(None).Complete(_user.Id, null, null, NewHttpContext());

        Assert.IsType<InternalServerError>(result);
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }
}

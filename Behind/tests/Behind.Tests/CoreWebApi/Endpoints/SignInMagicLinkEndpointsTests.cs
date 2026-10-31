using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SignInMagicLinkEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Overlap = TimeSpan.FromDays(1);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(1);
    private static readonly JwtSettings Jwt = new("issuer", "audience", new string('k', 64));
    private static readonly Uuid Actual = Uuid.CreateVersion7(Now.AddHours(-12));
    private static readonly Uuid Old = Uuid.CreateVersion7(Now.AddDays(-31));

    private readonly IMagicLinkCommands _magicLinks = Substitute.For<IMagicLinkCommands>();
    private readonly IMagicLinkQueries _queries = Substitute.For<IMagicLinkQueries>();
    private readonly ISessionCommands _sessions = Substitute.For<ISessionCommands>();
    private readonly IMagicLinkMailQueue _mailQueue = new MagicLinkMailQueue();
    private readonly TimeProvider _time = new FixedTimeProvider(Now);

    private static MagicLinkSettings Settings(Option<Uuid> old = default) =>
        new(Actual, old, Some(TimeSpan.FromDays(30)), Some(Overlap), Some(TimeSpan.FromMinutes(15)), Some(Cooldown));

    private static IMagicLinkSettingsStore StoreOf(Option<MagicLinkSettings> settings) =>
        new MagicLinkSettingsStore(settings);

    private static IOptions<SecurityConfiguration> Security(Option<JwtSettings> jwt) =>
        Options.Create(new SecurityConfiguration(false, None, jwt, None));

    private static ApplicationUser NewUser() =>
        new() { DisplayName = "User", Emails = [new Email("user@test.com")] };

    private static UserManager<ApplicationUser> UserManagerFor(ApplicationUser? user)
    {
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByEmailAsync(Arg.Any<string>()).Returns(user);
        manager.FindByIdAsync(Arg.Any<string>()).Returns(user);
        return manager;
    }

    private static IConfiguration FrontendConfigurationOf(string? url) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(url is null ? [] : new Dictionary<string, string?> { ["Frontend:Url"] = url })
            .Build();

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
        context.Request.Headers.UserAgent = "unit-test";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.test");
        return context;
    }

    private Task<IResult> RequestLink(
        ApplicationUser? user,
        Option<MagicLinkSettings> settings,
        IConfiguration? configuration = null,
        Option<JwtSettings>? jwt = null) =>
        SignInEndpoints.RequestMagicLink(
            UserManagerFor(user),
            Security(jwt ?? Some(Jwt)),
            StoreOf(settings),
            _magicLinks,
            _queries,
            _mailQueue,
            configuration ?? FrontendConfigurationOf(null),
            _time,
            NewHttpContext(),
            new SignInEndpoints.MagicLinkRequest("user@test.com"));

    private Task<IResult> Redeem(
        ApplicationUser? user,
        Option<MagicLinkSettings> settings,
        string? token,
        Option<JwtSettings>? jwt = null) =>
        SignInEndpoints.SignInMagicLink(
            UserManagerFor(user),
            Security(jwt ?? Some(Jwt)),
            StoreOf(settings),
            _magicLinks,
            _sessions,
            _time,
            NewHttpContext(),
            token);

    private static string TokenFor(Uuid secret, TokenNonce? nonce = null) =>
        MagicLinkCredential.Create(nonce ?? Guid.NewGuid(), secret, Jwt.SigningKey).Token;

    private List<MagicLinkMail> QueuedMails()
    {
        var mails = new List<MagicLinkMail>();
        while (_mailQueue.Reader.TryRead(out var mail))
        {
            mails.Add(mail);
        }

        return mails;
    }

    [Fact]
    public async Task RequestMagicLink_Disabled_ReturnsNotFound()
    {
        var result = await RequestLink(NewUser(), None);

        Assert.IsType<NotFound>(result);
        Assert.Empty(QueuedMails());
    }

    [Fact]
    public async Task RequestMagicLink_WithoutJwtSettings_ReturnsServerError()
    {
        var result = await RequestLink(NewUser(), Some(Settings()), jwt: None);

        Assert.IsType<InternalServerError>(result);
        Assert.Empty(QueuedMails());
    }

    [Fact]
    public async Task RequestMagicLink_UnknownEmail_ReturnsNoContentWithoutSideEffects()
    {
        var result = await RequestLink(null, Some(Settings()));

        Assert.IsType<NoContent>(result);
        await _magicLinks.DidNotReceiveWithAnyArgs().Issue(default, default!, default);
        Assert.Empty(QueuedMails());
    }

    [Fact]
    public async Task RequestMagicLink_WithinCooldown_ReturnsNoContentWithoutIssuingOrMailing()
    {
        var user = NewUser();
        _queries.HasIssuedSince(user.Id, Now - Cooldown, Arg.Any<CancellationToken>()).Returns(true);

        var result = await RequestLink(user, Some(Settings()));

        Assert.IsType<NoContent>(result);
        await _magicLinks.DidNotReceiveWithAnyArgs().Issue(default, default!, default);
        Assert.Empty(QueuedMails());
    }

    [Fact]
    public async Task RequestMagicLink_KnownEmail_IssuesLinkAndQueuesSignedToken()
    {
        var user = NewUser();
        MagicLink? issued = null;
        await _magicLinks.Issue(
            user.Id,
            Arg.Do<MagicLink>(link => issued = link),
            Arg.Any<CancellationToken>());

        var result = await RequestLink(user, Some(Settings()), FrontendConfigurationOf("https://app.test/"));

        Assert.IsType<NoContent>(result);
        Assert.NotNull(issued);
        Assert.Equal(Now.AddMinutes(15), issued.ExpiresAt);
        var mail = Assert.Single(QueuedMails());
        Assert.Equal("user@test.com", mail.Email);
        Assert.StartsWith($"https://app.test/{MagicLinkExtensions.SignInPath}?{MagicLinkExtensions.TokenParameter}=", mail.Link);
        var token = Uri.UnescapeDataString(mail.Link[(mail.Link.IndexOf('=') + 1)..]);
        var credential = MagicLinkCredential.TryParse(token).Match(() => throw new InvalidOperationException(), parsed => parsed);
        Assert.Equal(issued.Nonce, credential.Nonce);
        Assert.True(credential.IsSignedBy([Actual], Jwt.SigningKey));
    }

    [Fact]
    public async Task RequestMagicLink_WithoutFrontendUrl_UsesRequestBaseUri()
    {
        await RequestLink(NewUser(), Some(Settings()));

        var mail = Assert.Single(QueuedMails());
        Assert.StartsWith($"https://api.test/{MagicLinkExtensions.SignInPath}?", mail.Link);
    }

    [Fact]
    public async Task SignInMagicLink_WithoutJwtSettings_ReturnsServerError()
    {
        var result = await Redeem(NewUser(), Some(Settings()), TokenFor(Actual), None);

        Assert.IsType<InternalServerError>(result);
    }

    [Fact]
    public async Task SignInMagicLink_Disabled_ReturnsNotFound()
    {
        var result = await Redeem(NewUser(), None, TokenFor(Actual));

        Assert.IsType<NotFound>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    public async Task SignInMagicLink_MalformedToken_ReturnsUnauthorized(string? token)
    {
        var result = await Redeem(NewUser(), Some(Settings()), token);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await _magicLinks.DidNotReceiveWithAnyArgs().Consume(default!, default, default);
    }

    [Fact]
    public async Task SignInMagicLink_ForgedSignature_IsRejectedBeforeConsumingLink()
    {
        var forged = TokenFor(Uuid.CreateVersion7(Now));

        var result = await Redeem(NewUser(), Some(Settings()), forged);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await _magicLinks.DidNotReceiveWithAnyArgs().Consume(default!, default, default);
    }

    [Fact]
    public async Task SignInMagicLink_SignedWithAnotherPepper_IsRejected()
    {
        var token = MagicLinkCredential.Create(Guid.NewGuid(), Actual, "another-pepper").Token;

        var result = await Redeem(NewUser(), Some(Settings()), token);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await _magicLinks.DidNotReceiveWithAnyArgs().Consume(default!, default, default);
    }

    [Fact]
    public async Task SignInMagicLink_AlreadyConsumedOrExpired_ReturnsUnauthorized()
    {
        _magicLinks.Consume(Arg.Any<TokenNonce>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<Guid>>(None));

        var result = await Redeem(NewUser(), Some(Settings()), TokenFor(Actual));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task SignInMagicLink_UserNoLongerExists_ReturnsUnauthorized()
    {
        _magicLinks.Consume(Arg.Any<TokenNonce>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Some(Guid.NewGuid())));

        var result = await Redeem(null, Some(Settings()), TokenFor(Actual));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await _sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
    }

    [Fact]
    public async Task SignInMagicLink_ValidToken_ConsumesLinkAndStartsSession()
    {
        var user = NewUser();
        var nonce = new TokenNonce(Guid.NewGuid());
        var sessionId = Guid.NewGuid();
        _magicLinks.Consume(nonce, Now, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Some(user.Id)));
        _sessions.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(sessionId);

        var result = await Redeem(user, Some(Settings()), TokenFor(Actual, nonce));

        var json = Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result);
        var persisted = Assert.IsType<ValidSession>(_sessions.ReceivedCalls().Single().GetArguments()[1]);
        await _sessions.Received(1).CreateForUserId(user.Id, persisted, Arg.Any<CancellationToken>());
        Assert.Equal("10.1.2.3", persisted.RemoteAddress.Value);
        Assert.Equal("unit-test", persisted.UserAgent.Value);
        var access = new JwtSecurityTokenHandler().ReadJwtToken(json.Value!.AccessToken);
        Assert.Equal(sessionId.ToString(), access.Claims.Single(c => c.Type == SessionClaimTypes.SessionId).Value);
    }

    [Fact]
    public async Task SignInMagicLink_OldSecretDuringOverlap_IsStillAccepted()
    {
        var user = NewUser();
        _magicLinks.Consume(Arg.Any<TokenNonce>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Some(user.Id)));
        _sessions.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var result = await Redeem(user, Some(Settings(Some(Old))), TokenFor(Old));

        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(result);
    }

    [Fact]
    public async Task SignInMagicLink_DisablesCachingOfTheTokenResponse()
    {
        var httpContext = NewHttpContext();

        await SignInEndpoints.SignInMagicLink(
            UserManagerFor(NewUser()),
            Security(Some(Jwt)),
            StoreOf(Some(Settings())),
            _magicLinks,
            _sessions,
            _time,
            httpContext,
            null);

        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task SignInMagicLink_OldSecretAfterOverlap_IsRejected()
    {
        var expiredOverlap = Settings(Some(Old)) with { ActualSecret = Uuid.CreateVersion7(Now.AddDays(-2)) };

        var result = await Redeem(NewUser(), Some(expiredOverlap), TokenFor(Old));

        Assert.IsType<UnauthorizedHttpResult>(result);
        await _magicLinks.DidNotReceiveWithAnyArgs().Consume(default!, default, default);
    }
}

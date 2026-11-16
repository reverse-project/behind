using System.Security.Claims;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class TwoFactorProfileEndpointsTests : IDisposable
{
    private static readonly Guid SessionId = Guid.NewGuid();

    private readonly TwoFactorTestBed _bed = new();
    private readonly UserManager<ApplicationUser> _userManager = Substitute.For<UserManager<ApplicationUser>>(
        Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:Url"] = TwoFactorTestBed.Origin.ToString() })
        .Build();
    private readonly CapturingLogger<TwoFactorProfileEndpoints> _logger = new();
    private ApplicationUser _user = null!;

    public void Dispose() => _bed.Dispose();

    private async Task<ApplicationUser> SignedInUser()
    {
        _user = await _bed.SeedUser();
        _userManager.FindByIdAsync(_user.Id.ToString()).Returns(_user);

        return _user;
    }

    private static DefaultHttpContext SignedIn()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", SessionId.ToString())], "test"));

        return context;
    }

    private ISessionQueries Session(bool valid = true, bool impersonated = false)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(valid
                ? Some<(Session, Guid)>((
                    new ValidSession(
                        AccessToken.Create(new GuestUser(None)),
                        RefreshToken.Create(new GuestUser(None)),
                        "10.0.0.1",
                        "test-agent")
                    {
                        Impersonator = impersonated ? Some<User>(new GuestUser(None)) : None
                    },
                    _user.Id))
                : None);

        return queries;
    }

    private static T Value<T>(IResult result) => Assert.IsType<JsonHttpResult<T>>(result).Value!;

    [Fact]
    public async Task GetStatus_WithoutASession_IsUnauthorized()
    {
        await SignedInUser();

        var result = await TwoFactorProfileEndpoints.GetStatus(
            SignedIn(), Session(false), _userManager, _bed.Queries, _bed.Security, _logger);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task GetStatus_ImpersonationSessionIsForbidden()
    {
        await SignedInUser();

        var result = await TwoFactorProfileEndpoints.GetStatus(
            SignedIn(), Session(impersonated: true), _userManager, _bed.Queries, _bed.Security, _logger);

        Assert.IsType<ForbidHttpResult>(result);
        Assert.True(_logger.Has(LogLevel.Warning));
    }

    [Fact]
    public async Task GetStatus_ReportsWhatTheUserHasEnabled()
    {
        await SignedInUser();
        using var authenticator = await _bed.EnablePasskey(_user, "Laptop");
        await _bed.Enrollment.BeginTotp(_user);

        var result = await TwoFactorProfileEndpoints.GetStatus(
            SignedIn(), Session(), _userManager, _bed.Queries, _bed.Security, _logger);

        var status = Value<TwoFactorProfileEndpoints.StatusResponse>(result);
        Assert.True(status.Enabled);
        Assert.False(status.Required);
        Assert.False(status.TotpEnabled);
        Assert.True(status.TotpPending);
        Assert.Equal(20, status.RecoveryCodesRemaining);
        var passkey = Assert.Single(status.Passkeys);
        Assert.Equal("Laptop", passkey.Name);
    }

    [Fact]
    public async Task GetStatus_ReportsWhenTwoFactorIsRequired()
    {
        using var bed = new TwoFactorTestBed(true);
        await SignedInUser();

        var result = await TwoFactorProfileEndpoints.GetStatus(
            SignedIn(), Session(), _userManager, bed.Queries, bed.Security, _logger);

        var status = Value<TwoFactorProfileEndpoints.StatusResponse>(result);
        Assert.True(status.Required);
        Assert.False(status.Enabled);
        Assert.False(status.TotpPending);
    }

    [Fact]
    public async Task BeginTotp_ReturnsTheSetupAndConflictsWhenAlreadyEnabled()
    {
        await SignedInUser();

        var begun = await TwoFactorProfileEndpoints.BeginTotp(
            SignedIn(), Session(), _userManager, _bed.Enrollment, _logger);
        await _bed.EnableTotp(_user);
        var again = await TwoFactorProfileEndpoints.BeginTotp(
            SignedIn(), Session(), _userManager, _bed.Enrollment, _logger);

        Assert.Contains("<svg", Value<TotpSetup>(begun).QrCode);
        var conflict = Assert.IsType<Conflict<TwoFactorFailure>>(again);
        Assert.Equal(TwoFactorFailure.AlreadyEnabled, conflict.Value!.Code);
    }

    [Fact]
    public async Task ConfirmTotp_RightCodeEnablesItAndReturnsTheRecoveryCodes()
    {
        await SignedInUser();
        var setup = Value<TotpSetup>(await TwoFactorProfileEndpoints.BeginTotp(
            SignedIn(), Session(), _userManager, _bed.Enrollment, _logger));

        var result = await TwoFactorProfileEndpoints.ConfirmTotp(
            new TwoFactorProfileEndpoints.CodeRequest(_bed.CodeFor(setup.Secret)),
            SignedIn(),
            Session(),
            _userManager,
            _bed.Enrollment,
            _logger);

        Assert.Equal(20, Value<Enrolled>(result).RecoveryCodes.Count);
        Assert.True((await _bed.Queries.GetStatus(_user.Id)).TotpEnabled);
    }

    [Fact]
    public async Task ConfirmTotp_WrongCodeIsForbidden()
    {
        await SignedInUser();
        await TwoFactorProfileEndpoints.BeginTotp(SignedIn(), Session(), _userManager, _bed.Enrollment, _logger);

        var result = await TwoFactorProfileEndpoints.ConfirmTotp(
            new TwoFactorProfileEndpoints.CodeRequest("000000"),
            SignedIn(),
            Session(),
            _userManager,
            _bed.Enrollment,
            _logger);

        var json = Assert.IsType<JsonHttpResult<TwoFactorFailure>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
        Assert.Equal(TwoFactorFailure.InvalidCode, json.Value!.Code);
    }

    [Fact]
    public async Task RemoveTotp_RemovesItAndAnswersNotFoundWhenThereIsNone()
    {
        await SignedInUser();
        await _bed.EnableTotp(_user);

        var removed = await TwoFactorProfileEndpoints.RemoveTotp(
            SignedIn(), Session(), _userManager, _bed.Enrollment, _logger);
        var missing = await TwoFactorProfileEndpoints.RemoveTotp(
            SignedIn(), Session(), _userManager, _bed.Enrollment, _logger);

        Assert.IsType<NoContent>(removed);
        Assert.IsType<NotFound>(missing);
    }

    [Fact]
    public async Task RemoveTotp_TheLastMethodConflictsWhenTwoFactorIsRequired()
    {
        using var bed = new TwoFactorTestBed(true);
        _user = await bed.SeedUser();
        _userManager.FindByIdAsync(_user.Id.ToString()).Returns(_user);
        await bed.EnableTotp(_user);

        var result = await TwoFactorProfileEndpoints.RemoveTotp(
            SignedIn(), Session(), _userManager, bed.Enrollment, _logger);

        var conflict = Assert.IsType<Conflict<TwoFactorFailure>>(result);
        Assert.Equal(TwoFactorFailure.Required, conflict.Value!.Code);
    }

    [Fact]
    public async Task AddPasskey_RegistersWithTheTicketOfTheProfileStage()
    {
        await SignedInUser();
        using var authenticator = new VirtualAuthenticator();
        var challenge = Value<PasskeyRegistrationChallenge>(await TwoFactorProfileEndpoints.PasskeyOptions(
            SignedIn(), _configuration, Session(), _userManager, _bed.Enrollment, _logger));

        var result = await TwoFactorProfileEndpoints.AddPasskey(
            new TwoFactorProfileEndpoints.PasskeyRegistrationRequest(
                challenge.Ticket,
                authenticator.Register(challenge.Options, TwoFactorTestBed.OriginText),
                "Phone"),
            SignedIn(),
            _configuration,
            Session(),
            _userManager,
            _bed.Tickets,
            _bed.Enrollment,
            _logger);

        Assert.Equal(20, Value<Enrolled>(result).RecoveryCodes.Count);
        Assert.Equal("Phone", Assert.Single(await _bed.Queries.GetPasskeys(_user.Id)).Name);
    }

    [Fact]
    public async Task AddPasskey_RejectsTheTicketOfAnotherStageOrUser()
    {
        await SignedInUser();
        var other = await _bed.SeedUser("other@test.com");
        using var authenticator = new VirtualAuthenticator();
        var challenge = await _bed.Enrollment.BeginPasskey(TwoFactorTestBed.Origin, _user, TwoFactorStage.Setup, null);
        var foreign = await _bed.Enrollment.BeginPasskey(TwoFactorTestBed.Origin, other, TwoFactorStage.Profile, null);

        foreach (var ticket in new[] { challenge.Ticket, foreign.Ticket })
        {
            var result = await TwoFactorProfileEndpoints.AddPasskey(
                new TwoFactorProfileEndpoints.PasskeyRegistrationRequest(
                    ticket,
                    authenticator.Register(challenge.Options, TwoFactorTestBed.OriginText)),
                SignedIn(),
                _configuration,
                Session(),
                _userManager,
                _bed.Tickets,
                _bed.Enrollment,
                _logger);

            var json = Assert.IsType<JsonHttpResult<TwoFactorFailure>>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, json.StatusCode);
        }

        Assert.Empty(await _bed.Queries.GetPasskeys(_user.Id));
        Assert.Empty(await _bed.Queries.GetPasskeys(other.Id));
    }

    [Fact]
    public async Task RemovePasskey_RemovesItAndAnswersNotFoundForAnUnknownOne()
    {
        await SignedInUser();
        using var authenticator = await _bed.EnablePasskey(_user);
        var id = (await _bed.Queries.GetPasskeys(_user.Id)).Single().Id;

        var missing = await TwoFactorProfileEndpoints.RemovePasskey(
            Guid.NewGuid(), SignedIn(), Session(), _userManager, _bed.Enrollment, _logger);
        var removed = await TwoFactorProfileEndpoints.RemovePasskey(
            id, SignedIn(), Session(), _userManager, _bed.Enrollment, _logger);

        Assert.IsType<NotFound>(missing);
        Assert.IsType<NoContent>(removed);
        Assert.Empty(await _bed.Queries.GetPasskeys(_user.Id));
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_RequiresAnEnabledMethod()
    {
        await SignedInUser();

        var disabled = await TwoFactorProfileEndpoints.RegenerateRecoveryCodes(
            SignedIn(), Session(), _userManager, _bed.Queries, _bed.Enrollment, _logger);
        await _bed.EnableTotp(_user);
        var enabled = await TwoFactorProfileEndpoints.RegenerateRecoveryCodes(
            SignedIn(), Session(), _userManager, _bed.Queries, _bed.Enrollment, _logger);

        var conflict = Assert.IsType<Conflict<TwoFactorFailure>>(disabled);
        Assert.Equal(TwoFactorFailure.NotEnabled, conflict.Value!.Code);
        Assert.Equal(20, Value<Enrolled>(enabled).RecoveryCodes.Count);
    }
}

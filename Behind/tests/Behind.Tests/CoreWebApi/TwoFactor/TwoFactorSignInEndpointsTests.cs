using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class TwoFactorSignInEndpointsTests : IAsyncLifetime
{
    private readonly TwoFactorTestBed _bed = new();
    private readonly RecordingSignIn _signIn = new();
    private readonly RecordingEmail _email = new();
    private readonly UserManager<ApplicationUser> _userManager = Substitute.For<UserManager<ApplicationUser>>(
        Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
    private WebApplication _application = null!;
    private HttpClient _client = null!;
    private ApplicationUser _user = null!;
    private Guid _nonce;

    public async Task InitializeAsync()
    {
        _user = await _bed.SeedUser();
        _userManager.FindByIdAsync(_user.Id.ToString()).Returns(_user);
        var link = MagicLink.Create(_bed.Time.GetUtcNow(), TwoFactorTickets.Lifetime, MagicLinkFunction.TwoFactorChallenge);
        await new MagicLinkCommands(_bed.Context).Issue(_user.Id, link);
        _nonce = link.Nonce.Nonce;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Frontend:Url"] = TwoFactorTestBed.Origin.ToString();
        builder.Services.AddSingleton<TimeProvider>(_bed.Time);
        builder.Services.AddSingleton(_bed.DataProtection);
        builder.Services.AddSingleton(_userManager);
        builder.Services.AddSingleton(Substitute.For<ISessionQueries>());
        builder.Services.AddSingleton(_bed.Security);
        builder.Services.AddSingleton<IEmailSender>(_email);
        builder.Services.AddScoped<ITwoFactorSignIn>(_ => _signIn);
        builder.Services.AddScoped<CoreContextSqlite>(_ => _bed.Factory.CreateContext());
        builder.Services.AddScoped<AbstractCoreContext>(provider => provider.GetRequiredService<CoreContextSqlite>());
        builder.Services.AddScoped<ITwoFactorQueries, TwoFactorQueries>();
        builder.Services.AddScoped<ITwoFactorCommands, TwoFactorCommands>();
        builder.Services.AddScoped<IEmailQueries, EmailQueries>();
        builder.Services.AddScoped<IMagicLinkCommands, MagicLinkCommands>();
        builder.Services.AddScoped<IMagicLinkQueries, MagicLinkQueries>();
        builder.Services.AddTwoFactor();

        _application = builder.Build();
        _application.MapTwoFactorEndpoints();
        await _application.StartAsync();

        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        _bed.Dispose();
    }

    private sealed class RecordingSignIn : ITwoFactorSignIn
    {
        public List<(Guid UserId, MagicLinkFunction? Function, IReadOnlyList<string>? RecoveryCodes)> Completed { get; } = [];
        public bool Reject { get; set; }

        public Task<IResult> Complete(
            Guid userId,
            MagicLinkFunction? function,
            IReadOnlyList<string>? recoveryCodes,
            HttpContext httpContext,
            TwoFactorTicket? ticket = null)
        {
            if (Reject)
            {
                return Task.FromResult<IResult>(Results.Unauthorized());
            }

            Completed.Add((userId, function, recoveryCodes));

            return Task.FromResult(Results.Json(new { signedIn = true, recoveryCodes }));
        }
    }

    private sealed class RecordingEmail : IEmailSender
    {
        public string Body { get; private set; } = string.Empty;

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Body = htmlMessage;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EmailFactor_SelectsConfirmedAddressAndVerifiesDeliveredCode()
    {
        await _bed.EnableTotp(_user);
        var response = await _bed.Gate.Challenge(_user.Id, null);
        var ticket = response.Match(() => throw new InvalidOperationException(), found => found.Ticket);
        var addresses = await Post("email/addresses", new { ticket });
        Assert.Equal(HttpStatusCode.OK, addresses.StatusCode);
        var address = (await Body(addresses))["addresses"]![0]!;
        Assert.Equal("a***@test.com", (string?)address["value"]);

        var delivery = await Post("email", new { ticket, addressId = (string)address["id"]! });
        Assert.Equal(HttpStatusCode.OK, delivery.StatusCode);
        var deliveryTicket = (string)(await Body(delivery))["ticket"]!;
        var code = Regex.Match(_email.Body, @"\b\d{6}\b").Value;
        Assert.Equal(6, code.Length);
        var verified = await Post("email/verify", new { ticket = deliveryTicket, code });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Equal(_user.Id, Assert.Single(_signIn.Completed).UserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post("email/verify", new { ticket = deliveryTicket, code })).StatusCode);
    }

    [Fact]
    public async Task EmailFactor_InvalidAttemptsRemainLimitedAcrossTransactions()
    {
        await _bed.EnableTotp(_user);
        var response = await _bed.Gate.Challenge(_user.Id, null);
        var ticket = response.Match(() => throw new InvalidOperationException(), found => found.Ticket);
        var addresses = await Body(await Post("email/addresses", new { ticket }));
        var delivery = await Body(await Post("email", new { ticket, addressId = (string)addresses["addresses"]![0]!["id"]! }));
        var deliveryTicket = (string)delivery["ticket"]!;

        for (var attempt = 0; attempt < EmailTwoFactor.VerificationLimit; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await Post("email/verify", new { ticket = deliveryTicket, code = "not-a-code" })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Post("email/verify", new { ticket = deliveryTicket, code = "not-a-code" })).StatusCode);
    }

    [Fact]
    public async Task CompletedChallenge_CannotBeginSetupOrConsumeRecoveryCodes()
    {
        await _bed.EnableTotp(_user);
        var codes = await _bed.RecoveryCodes.Issue(_user.Id);
        var verifyTicket = Ticket();
        var setupTicket = Ticket(TwoFactorStage.Setup);
        Assert.True(await new MagicLinkCommands(_bed.Context).ConsumeForFunction(
            _nonce, _user.Id, MagicLinkFunction.TwoFactorChallenge, _bed.Time.GetUtcNow()));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post("recovery", new { ticket = verifyTicket, code = codes[0] })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("setup/totp", new { ticket = setupTicket })).StatusCode);
        Assert.Equal(codes.Count, (await _bed.Queries.GetRecoveryCodes(_user.Id)).Count);
        Assert.Empty(_signIn.Completed);
    }

    [Fact]
    public async Task CompletedChallenge_CannotConfirmPendingSetup()
    {
        var ticket = Ticket(TwoFactorStage.Setup);
        Assert.Equal(HttpStatusCode.OK, (await Post("setup/totp", new { ticket })).StatusCode);
        var code = _bed.CodeFor(await _bed.PendingSecret(_user.Id));
        Assert.True(await new MagicLinkCommands(_bed.Context).ConsumeForFunction(
            _nonce, _user.Id, MagicLinkFunction.TwoFactorChallenge, _bed.Time.GetUtcNow()));

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("setup/totp/confirm", new { ticket, code })).StatusCode);
        Assert.False((await _bed.Queries.GetStatus(_user.Id)).IsEnabled);
    }

    [Fact]
    public async Task RejectedCompletion_RollsBackRecoveryCodeRedemption()
    {
        await _bed.EnableTotp(_user);
        var codes = await _bed.RecoveryCodes.Issue(_user.Id);
        var ticket = Ticket();
        _signIn.Reject = true;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("recovery", new { ticket, code = codes[0] })).StatusCode);
        Assert.Equal(codes.Count, (await _bed.Queries.GetRecoveryCodes(_user.Id)).Count);

        _signIn.Reject = false;
        Assert.Equal(HttpStatusCode.OK, (await Post("recovery", new { ticket, code = codes[0] })).StatusCode);
        Assert.Single(_signIn.Completed);
    }

    private string Ticket(
        TwoFactorStage stage = TwoFactorStage.Verify,
        MagicLinkFunction? function = null,
        string? ceremony = null)
        => _bed.Tickets.Seal(_bed.Tickets.Create(_user.Id, stage, function, ceremony) with { Nonce = _nonce });

    private Task<HttpResponseMessage> Post(string route, object body) =>
        _client.PostAsJsonAsync($"{TwoFactorRoutesExtensions.SignInRoute}/{route}", body);

    private static async Task<JsonNode> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonNode>())!;

    private static async Task<string> FailureOf(HttpResponseMessage response) =>
        (string)(await Body(response))["code"]!;

    [Fact]
    public async Task Totp_RightCodeCompletesTheSignInWithTheFunctionOfTheTicket()
    {
        await _bed.EnableTotp(_user);
        var secret = await _bed.PendingSecret(_user.Id);

        var response = await Post(
            "totp",
            new { ticket = Ticket(function: MagicLinkFunction.LostPassword), code = _bed.CodeFor(secret) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = Assert.Single(_signIn.Completed);
        Assert.Equal(_user.Id, completed.UserId);
        Assert.Equal(MagicLinkFunction.LostPassword, completed.Function);
        Assert.Null(completed.RecoveryCodes);
    }

    [Fact]
    public async Task Totp_WrongCodeIsForbiddenAndDoesNotSignIn()
    {
        await _bed.EnableTotp(_user);

        var response = await Post("totp", new { ticket = Ticket(), code = "000000" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TwoFactorFailure.InvalidCode, await FailureOf(response));
        Assert.Empty(_signIn.Completed);
    }

    [Fact]
    public async Task Totp_ReplayedCodeIsForbidden()
    {
        await _bed.EnableTotp(_user);
        var code = _bed.CodeFor(await _bed.PendingSecret(_user.Id));

        var first = await Post("totp", new { ticket = Ticket(), code });
        var second = await Post("totp", new { ticket = Ticket(), code });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        Assert.Single(_signIn.Completed);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    public async Task Totp_InvalidTicketIsUnauthorized(string ticket)
    {
        var response = await Post("totp", new { ticket, code = "123456" });

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest);
        Assert.Empty(_signIn.Completed);
    }

    [Fact]
    public async Task Totp_UnknownTicketIsUnauthorizedWithTheTicketInvalidCode()
    {
        var response = await Post("totp", new { ticket = "garbage", code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(TwoFactorFailure.TicketInvalid, await FailureOf(response));
    }

    [Fact]
    public async Task Totp_ExpiredTicketIsUnauthorized()
    {
        await _bed.EnableTotp(_user);
        var ticket = Ticket();
        var code = _bed.CodeFor(await _bed.PendingSecret(_user.Id));

        _bed.Time.Advance(TwoFactorTickets.Lifetime + TimeSpan.FromSeconds(1));
        var response = await Post("totp", new { ticket, code = _bed.CodeFor(await _bed.PendingSecret(_user.Id)) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEmpty(code);
        Assert.Empty(_signIn.Completed);
    }

    [Fact]
    public async Task Totp_TicketOfTheSetupStageIsUnauthorized()
    {
        await _bed.EnableTotp(_user);
        var code = _bed.CodeFor(await _bed.PendingSecret(_user.Id));

        var response = await Post("totp", new { ticket = Ticket(TwoFactorStage.Setup), code });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_signIn.Completed);
    }

    [Fact]
    public async Task Totp_MissingCodeIsABadRequest()
    {
        var response = await Post("totp", new { ticket = Ticket() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recovery_RightCodeSignsInAndConsumesIt()
    {
        await _bed.EnableTotp(_user);
        var code = (await _bed.RecoveryCodes.Issue(_user.Id))[0];

        var first = await Post("recovery", new { ticket = Ticket(), code });
        var second = await Post("recovery", new { ticket = Ticket(), code });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        Assert.Equal(TwoFactorFailure.InvalidCode, await FailureOf(second));
        var completed = Assert.Single(_signIn.Completed);
        Assert.True(completed.RecoveryCodes is null or { Count: 0 });
        Assert.Equal(19, (await _bed.Queries.GetRecoveryCodes(_user.Id)).Count);
    }

    [Fact]
    public async Task Recovery_TheLastCodeHandsOverAFreshBunch()
    {
        await _bed.EnableTotp(_user);
        var codes = await _bed.RecoveryCodes.Issue(_user.Id);

        foreach (var code in codes.Take(19))
        {
            await _bed.RecoveryCodes.Redeem(_user.Id, code);
        }

        var response = await Post("recovery", new { ticket = Ticket(), code = codes[19] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = Assert.Single(_signIn.Completed);
        Assert.Equal(20, completed.RecoveryCodes!.Count);
        Assert.Equal(20, (await _bed.Queries.GetRecoveryCodes(_user.Id)).Count);
    }

    [Fact]
    public async Task PasskeyOptions_WithoutPasskeysIsNotFound()
    {
        var response = await Post("passkey/options", new { ticket = Ticket() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Passkey_AuthenticatorCompletesTheSignIn()
    {
        using var authenticator = await _bed.EnablePasskey(_user);

        var optionsResponse = await Post("passkey/options", new { ticket = Ticket(function: MagicLinkFunction.SignIn) });
        var challenge = await Body(optionsResponse);
        var options = AssertionOptions.FromJson(challenge["options"]!.ToJsonString());
        var response = await Post(
            "passkey",
            new
            {
                ticket = (string)challenge["ticket"]!,
                credential = authenticator.Assert(options, TwoFactorTestBed.OriginText)
            });

        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = Assert.Single(_signIn.Completed);
        Assert.Equal(_user.Id, completed.UserId);
        Assert.Equal(MagicLinkFunction.SignIn, completed.Function);
    }

    [Fact]
    public async Task Passkey_PrimaryCredentialIsExcludedAndAnotherPasskeyCanComplete()
    {
        using var primary = await _bed.EnablePasskey(_user);
        using var second = await _bed.EnablePasskey(_user);
        var original = _bed.Tickets.Open(Ticket()).Match(() => throw new InvalidOperationException(), found => found);
        var ticket = _bed.Tickets.Seal(original with
        {
            PrimaryCredentialId = System.Buffers.Text.Base64Url.EncodeToString(primary.CredentialId)
        });

        var optionsResponse = await Post("passkey/options", new { ticket });
        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        var challenge = await Body(optionsResponse);
        var options = AssertionOptions.FromJson(challenge["options"]!.ToJsonString());
        Assert.DoesNotContain(options.AllowCredentials, credential => credential.Id.SequenceEqual(primary.CredentialId));
        Assert.Contains(options.AllowCredentials, credential => credential.Id.SequenceEqual(second.CredentialId));

        var primaryResponse = await Post("passkey", new
        {
            ticket = (string)challenge["ticket"]!,
            credential = primary.Assert(options, TwoFactorTestBed.OriginText)
        });
        Assert.Equal(HttpStatusCode.Forbidden, primaryResponse.StatusCode);
        Assert.Empty(_signIn.Completed);

        var secondResponse = await Post("passkey", new
        {
            ticket = (string)challenge["ticket"]!,
            credential = second.Assert(options, TwoFactorTestBed.OriginText)
        });
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Single(_signIn.Completed);
    }

    [Fact]
    public async Task Passkey_ForeignAuthenticatorIsForbidden()
    {
        using var authenticator = await _bed.EnablePasskey(_user);
        using var intruder = new VirtualAuthenticator();

        var challenge = await Body(await Post("passkey/options", new { ticket = Ticket() }));
        var options = AssertionOptions.FromJson(challenge["options"]!.ToJsonString());
        var response = await Post(
            "passkey",
            new
            {
                ticket = (string)challenge["ticket"]!,
                credential = intruder.Assert(options, TwoFactorTestBed.OriginText, claimedCredentialId: authenticator.CredentialId)
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_signIn.Completed);
    }

    [Fact]
    public async Task Passkey_TicketWithoutACeremonyIsForbidden()
    {
        using var authenticator = await _bed.EnablePasskey(_user);
        var options = _bed.Ceremonies.BeginAssertion(TwoFactorTestBed.Origin, [new PublicKeyCredentialDescriptor(authenticator.CredentialId)]);

        var response = await Post(
            "passkey",
            new { ticket = Ticket(), credential = authenticator.Assert(options, TwoFactorTestBed.OriginText) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_signIn.Completed);
    }

    [Fact]
    public async Task SetupTotp_ReturnsTheSecretAndTheQrCode()
    {
        var response = await Post("setup/totp", new { ticket = Ticket(TwoFactorStage.Setup) });

        var body = await Body(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(await _bed.PendingSecret(_user.Id), (string)body["secret"]!);
        Assert.Contains("<svg", (string)body["qrCode"]!);
        Assert.StartsWith("otpauth://totp/", (string)body["uri"]!);
    }

    [Fact]
    public async Task SetupTotp_VerifyTicketIsUnauthorized()
    {
        var response = await Post("setup/totp", new { ticket = Ticket(TwoFactorStage.Verify) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetupTotp_WhenAlreadyEnabledIsAConflict()
    {
        await _bed.EnableTotp(_user);

        var response = await Post("setup/totp", new { ticket = Ticket(TwoFactorStage.Setup) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(TwoFactorFailure.AlreadyEnabled, await FailureOf(response));
    }

    [Fact]
    public async Task ConfirmTotp_EnablesItAndSignsInWithTheFirstRecoveryCodes()
    {
        await Post("setup/totp", new { ticket = Ticket(TwoFactorStage.Setup) });
        var code = _bed.CodeFor(await _bed.PendingSecret(_user.Id));

        var response = await Post(
            "setup/totp/confirm",
            new { ticket = Ticket(TwoFactorStage.Setup, MagicLinkFunction.LostPassword), code });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = Assert.Single(_signIn.Completed);
        Assert.Equal(20, completed.RecoveryCodes!.Count);
        Assert.Equal(MagicLinkFunction.LostPassword, completed.Function);
        Assert.True((await _bed.Queries.GetStatus(_user.Id)).TotpEnabled);
    }

    [Fact]
    public async Task ConfirmTotp_WrongCodeIsForbidden()
    {
        await Post("setup/totp", new { ticket = Ticket(TwoFactorStage.Setup) });

        var response = await Post("setup/totp/confirm", new { ticket = Ticket(TwoFactorStage.Setup), code = "000000" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_signIn.Completed);
        Assert.False((await _bed.Queries.GetStatus(_user.Id)).TotpEnabled);
    }

    [Fact]
    public async Task SetupPasskey_RegistersAndSignsInWithTheFirstRecoveryCodes()
    {
        using var authenticator = new VirtualAuthenticator();

        var optionsResponse = await Post("setup/passkey/options", new { ticket = Ticket(TwoFactorStage.Setup) });
        var challenge = await Body(optionsResponse);
        var options = CredentialCreateOptions.FromJson(challenge["options"]!.ToJsonString());
        var response = await Post(
            "setup/passkey",
            new
            {
                ticket = (string)challenge["ticket"]!,
                name = "Security key",
                credential = authenticator.Register(options, TwoFactorTestBed.OriginText)
            });

        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(20, Assert.Single(_signIn.Completed).RecoveryCodes!.Count);
        Assert.Equal("Security key", Assert.Single(await _bed.Queries.GetPasskeys(_user.Id)).Name);
    }

    [Fact]
    public async Task SetupPasskey_ForeignOriginIsForbidden()
    {
        using var authenticator = new VirtualAuthenticator();

        var challenge = await Body(await Post("setup/passkey/options", new { ticket = Ticket(TwoFactorStage.Setup) }));
        var options = CredentialCreateOptions.FromJson(challenge["options"]!.ToJsonString());
        var response = await Post(
            "setup/passkey",
            new
            {
                ticket = (string)challenge["ticket"]!,
                credential = authenticator.Register(options, "https://evil.test")
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_signIn.Completed);
        Assert.Empty(await _bed.Queries.GetPasskeys(_user.Id));
    }

    [Fact]
    public async Task SetupPasskey_TooLongNameIsABadRequest()
    {
        using var authenticator = new VirtualAuthenticator();
        var challenge = await Body(await Post("setup/passkey/options", new { ticket = Ticket(TwoFactorStage.Setup) }));
        var options = CredentialCreateOptions.FromJson(challenge["options"]!.ToJsonString());

        var response = await Post(
            "setup/passkey",
            new
            {
                ticket = (string)challenge["ticket"]!,
                name = new string('x', UserPasskey.MaxNameLength + 1),
                credential = authenticator.Register(options, TwoFactorTestBed.OriginText)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Setup_UnknownUserOfTheTicketIsUnauthorized()
    {
        _userManager.FindByIdAsync(_user.Id.ToString()).Returns((ApplicationUser?)null);

        var response = await Post("setup/totp", new { ticket = Ticket(TwoFactorStage.Setup) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

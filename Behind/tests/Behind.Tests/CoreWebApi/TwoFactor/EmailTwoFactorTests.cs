using System.Text.RegularExpressions;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class EmailTwoFactorTests : IDisposable
{
    private readonly TwoFactorTestBed _bed = new(required: true);
    private readonly RecordingSender _sender = new();

    public void Dispose() => _bed.Dispose();

    private TwoFactorChallengeNonces Nonces() =>
        new(new MagicLinkCommands(_bed.Context), new MagicLinkQueries(_bed.Context), _bed.Time);

    private EmailTwoFactor Service(AccountsConfiguration? accounts = null) =>
        new(
            _bed.Tickets,
            _bed.Queries,
            new MagicLinkCommands(_bed.Context),
            Nonces(),
            _sender,
            FixedOptionsSnapshot.Of(accounts ?? new AccountsConfiguration()),
            _bed.Security,
            _bed.Time,
            NullLogger<EmailTwoFactor>.Instance);

    private Task<TwoFactorTicket> Ticket(ApplicationUser user, MagicLinkFunction? function = null) =>
        Nonces().Issue(_bed.Tickets.Create(user.Id, TwoFactorStage.Setup, function));

    private TwoFactorTicket Open(string? token) =>
        _bed.Tickets.Open(token).Match(() => throw new InvalidOperationException(), found => found);

    private async Task<(ApplicationUser User, TwoFactorTicket Parent, TwoFactorTicket Delivery)> Begin()
    {
        var user = await _bed.SeedUser();
        var parent = await Ticket(user);
        var addresses = await Service().GetAddresses(user, parent);
        var result = await Service().Begin(user, parent, Assert.Single(addresses.Addresses).Id);
        Assert.Null(result.Error);
        return (user, parent, Open(result.Ticket));
    }

    [Fact]
    public async Task Addresses_OnlyExposeMaskedConfirmedAccountSelections()
    {
        var user = await _bed.SeedUser();
        user.Emails =
        [
            new Email("zara@example.test", TwoFactorTestBed.Start),
            new Email("ada@example.test", TwoFactorTestBed.Start),
            new Email("unconfirmed@example.test")
        ];
        var ticket = await Ticket(user);

        var result = await Service().GetAddresses(user, ticket);

        Assert.Null(result.Error);
        Assert.Equal(["a***@example.test", "z***@example.test"], result.Addresses.Select(address => address.Display));
        Assert.All(result.Addresses, address => Assert.DoesNotContain("@", address.Id));
        var selected = result.Addresses[1];
        var delivery = await Service().Begin(user, ticket, selected.Id);
        Assert.Null(delivery.Error);
        Assert.Equal("zara@example.test", _sender.Address);
    }

    [Fact]
    public async Task Begin_RejectsRawOrUnconfirmedAddresses()
    {
        var user = await _bed.SeedUser();
        var ticket = await Ticket(user);
        var result = await Service().Begin(user, ticket, "ada@test.com");

        Assert.Equal(TwoFactorFailure.TicketInvalid, result.Error);
        Assert.Equal(0, _sender.Count);
    }

    [Fact]
    public async Task Begin_RejectsSelectionsBoundToAnotherChallenge()
    {
        var user = await _bed.SeedUser();
        var first = await Ticket(user);
        var second = await Ticket(user);
        var address = Assert.Single((await Service().GetAddresses(user, first)).Addresses);

        Assert.Equal(
            TwoFactorFailure.TicketInvalid,
            (await Service().Begin(user, second, address.Id)).Error);
    }

    [Theory]
    [InlineData(MagicLinkFunction.SignIn)]
    [InlineData(MagicLinkFunction.LostPassword)]
    [InlineData(MagicLinkFunction.PasswordReset)]
    public async Task Addresses_NeverOfferEmailAfterEmailAuthentication(MagicLinkFunction function)
    {
        var user = await _bed.SeedUser();
        var result = await Service().GetAddresses(user, await Ticket(user, function));

        Assert.Empty(result.Addresses);
        Assert.Equal(EmailTwoFactor.Unavailable, result.Error);
    }

    [Fact]
    public async Task Addresses_RejectMissingPrimaryChallengeNonce()
    {
        var user = await _bed.SeedUser();
        var result = await Service().GetAddresses(user, _bed.Tickets.Create(user.Id, TwoFactorStage.Setup));

        Assert.Empty(result.Addresses);
        Assert.Equal(EmailTwoFactor.Unavailable, result.Error);
    }

    [Fact]
    public async Task Addresses_RejectDisabledEmailFactor()
    {
        var user = await _bed.SeedUser();
        var result = await Service(new AccountsConfiguration { EmailTwoFactor = false })
            .GetAddresses(user, await Ticket(user));

        Assert.Equal(EmailTwoFactor.Unavailable, result.Error);
    }

    [Fact]
    public async Task Addresses_DoNotProtectPreviouslyUnprotectedUsers()
    {
        using var bed = new TwoFactorTestBed();
        var user = await bed.SeedUser();
        var nonces = new TwoFactorChallengeNonces(
            new MagicLinkCommands(bed.Context), new MagicLinkQueries(bed.Context), bed.Time);
        var service = new EmailTwoFactor(
            bed.Tickets, bed.Queries, new MagicLinkCommands(bed.Context), nonces, _sender,
            FixedOptionsSnapshot.Of(new AccountsConfiguration()), bed.Security, bed.Time, NullLogger<EmailTwoFactor>.Instance);
        var ticket = await nonces.Issue(bed.Tickets.Create(user.Id, TwoFactorStage.Setup));

        Assert.Equal(EmailTwoFactor.Unavailable, (await service.GetAddresses(user, ticket)).Error);
    }

    [Fact]
    public async Task Verify_ConsumesDeliveryOnceAndPreservesBaseForSessionCompletion()
    {
        var (user, parent, delivery) = await Begin();
        var result = await Service().Verify(user, delivery, _sender.Code);

        Assert.Null(result.Error);
        Assert.Equal(parent, result.Ticket);
        Assert.True(await Nonces().Consume(result.Ticket!));
        Assert.False(await Nonces().IsUsable(parent));
        Assert.False(await Nonces().Consume(parent));
        Assert.NotNull((await Service().Verify(user, delivery, _sender.Code)).Error);
    }

    [Fact]
    public async Task Verify_RequiredSetupCompletesOnlyOneSessionAcrossFactorTickets()
    {
        var (user, parent, delivery) = await Begin();
        var verified = await Service().Verify(user, delivery, _sender.Code);
        Assert.Equal(parent, verified.Ticket);
        var users = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        users.FindByIdAsync(user.Id.ToString()).Returns(user);
        var sessions = Substitute.For<ISessionCommands>();
        sessions.CreateForUserId(Arg.Any<Guid>(), Arg.Any<ValidSession>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var signIn = new TwoFactorSignIn(users, security, sessions, Substitute.For<IWallOfShameQueries>(),
            Substitute.For<IPasswordResetGrants>(), new MagicLinkCommands(_bed.Context), _bed.Time,
            NullLogger<TwoFactorSignIn>.Instance);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(
            await signIn.Complete(user.Id, null, null, context, verified.Ticket));
        var replay = await signIn.Complete(user.Id, null, null, context, parent with { Ceremony = "another-factor" });
        Assert.Equal(401, Assert.IsType<JsonHttpResult<TwoFactorFailure>>(replay).StatusCode);
        await sessions.Received(1).CreateForUserId(user.Id, Arg.Any<ValidSession>(), Arg.Any<CancellationToken>());
        var next = await _bed.Gate.Challenge(user.Id, null);
        Assert.True(next.Match(() => false, found => found.SetupRequired));
    }

    [Fact]
    public async Task Delivery_PreservesPrimaryPasskeyBindingAndCapsExpiryToBaseTicket()
    {
        var user = await _bed.SeedUser();
        var parent = await Nonces().Issue(
            _bed.Tickets.Create(user.Id, TwoFactorStage.Setup) with { PrimaryCredentialId = "primary-credential" });
        _bed.Time.Advance(TimeSpan.FromMinutes(4));
        var address = Assert.Single((await Service().GetAddresses(user, parent)).Addresses);

        var result = await Service().Begin(user, parent, address.Id);
        var delivery = Open(result.Ticket);

        Assert.Null(result.Error);
        Assert.Equal(parent.Expires, delivery.Expires);
        Assert.Equal(parent.Nonce, delivery.Nonce);
        Assert.Equal(parent.PrimaryCredentialId, delivery.PrimaryCredentialId);
        Assert.Equal(parent, (await Service().Verify(user, delivery, _sender.Code)).Ticket);
    }

    [Fact]
    public async Task Verify_RejectsReplayedDeliveryBeforeBaseConsumption()
    {
        var (user, _, delivery) = await Begin();
        Assert.Null((await Service().Verify(user, delivery, _sender.Code)).Error);

        Assert.Equal(TwoFactorFailure.InvalidCode, (await Service().Verify(user, delivery, _sender.Code)).Error);
    }

    [Fact]
    public async Task Verify_RejectsDeliveryAfterAnotherFactorConsumesBase()
    {
        var (user, parent, delivery) = await Begin();
        Assert.True(await Nonces().Consume(parent));

        Assert.NotNull((await Service().Verify(user, delivery, _sender.Code)).Error);
    }

    [Fact]
    public async Task Verify_RejectsExpiredDeliveryWithoutExtendingParentLifetime()
    {
        var (user, parent, delivery) = await Begin();
        Assert.Equal(_bed.Time.GetUtcNow().Add(EmailTwoFactor.Lifetime), delivery.Expires);
        Assert.True(delivery.Expires <= parent.Expires);
        _bed.Time.Advance(EmailTwoFactor.Lifetime);

        Assert.NotNull((await Service().Verify(user, delivery, _sender.Code)).Error);
    }

    [Fact]
    public async Task Verify_RejectsNoLongerConfirmedSelectedAddress()
    {
        var (user, _, delivery) = await Begin();
        user.Emails = [new Email("ada@test.com")];

        Assert.NotNull((await Service().Verify(user, delivery, _sender.Code)).Error);
    }

    [Fact]
    public async Task Verify_RejectsAnotherUser()
    {
        var (_, _, delivery) = await Begin();
        var other = await _bed.SeedUser("other@test.com");

        Assert.NotNull((await Service().Verify(other, delivery, _sender.Code)).Error);
    }

    [Fact]
    public async Task Verify_AttemptsRemainLimitedAcrossServiceRecreation()
    {
        var (user, _, delivery) = await Begin();
        for (var attempt = 0; attempt < EmailTwoFactor.VerificationLimit; attempt++)
        {
            Assert.Equal(TwoFactorFailure.InvalidCode, (await Service().Verify(user, delivery, "wrong")).Error);
        }

        Assert.Equal(EmailTwoFactor.RateLimited, (await Service().Verify(user, delivery, _sender.Code)).Error);
    }

    [Fact]
    public async Task Begin_ResendsFromDeliveryTicketWithoutExtendingBaseExpiry()
    {
        var (user, parent, delivery) = await Begin();
        var address = Assert.Single((await Service().GetAddresses(user, delivery)).Addresses);
        Assert.Equal(EmailTwoFactor.RateLimited, (await Service().Begin(user, delivery, address.Id)).Error);
        _bed.Time.Advance(EmailTwoFactor.DeliveryWindow);

        var result = await Service().Begin(user, delivery, address.Id);

        Assert.Null(result.Error);
        Assert.Equal(2, _sender.Count);
        Assert.Equal(parent.Nonce, Open(result.Ticket).Nonce);
        Assert.True(Open(result.Ticket).Expires <= parent.Expires);
    }

    [Fact]
    public async Task Begin_DeliveryFailureReturnsExplicitError()
    {
        _sender.Fail = true;
        var user = await _bed.SeedUser();
        var ticket = await Ticket(user);
        var address = Assert.Single((await Service().GetAddresses(user, ticket)).Addresses);

        Assert.Equal(EmailTwoFactor.DeliveryFailed, (await Service().Begin(user, ticket, address.Id)).Error);
    }

    private sealed class RecordingSender : IEmailSender
    {
        public string? Address { get; private set; }
        public string Code { get; private set; } = "";
        public int Count { get; private set; }
        public bool Fail { get; set; }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (Fail)
            {
                throw new IOException("Mail transport unavailable.");
            }

            Address = email;
            Code = Regex.Match(htmlMessage, @"<strong>(\d{6})</strong>").Groups[1].Value;
            Count++;
            return Task.CompletedTask;
        }
    }
}

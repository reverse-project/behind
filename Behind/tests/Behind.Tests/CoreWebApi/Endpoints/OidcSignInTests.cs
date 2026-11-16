using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.Oidc;
using Behind.Core.WebApi.TwoFactor;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class OidcSignInTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static OidcProviderConfiguration Provider => new()
    {
        Id = "company", DisplayName = "Company", Authority = "https://id.example.test",
        ClientId = "behind", LinkByVerifiedEmail = true,
        RoleMappings = new() { ["writers"] = ["Editor"] }, AutoCreateRoles = true
    };
    private static OidcIdentity Identity => new("company", "https://id.example.test", "subject",
        "user@example.test", true, "User", ["writers", "reader"]);
    private static ApplicationUser User => new()
    {
        Emails = [new Email("user@example.test", Now)], DisplayName = "Existing"
    };

    private static UserManager<ApplicationUser> Users() =>
        Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

    private static OidcSignInService Service(IOidcAccountStore store, UserManager<ApplicationUser> users,
        OidcProviderConfiguration? provider = null, AccountsConfiguration? accounts = null,
        IAccessSettingsQueries? access = null) =>
        new(new(new() { Providers = [provider ?? Provider] }), store, users,
            Options.Create(accounts ?? new()), access ?? Substitute.For<IAccessSettingsQueries>(),
            new FixedTimeProvider(Now), NullLogger<OidcSignInService>.Instance);

    [Fact]
    public async Task LinksVerifiedEmailAndSynchronizesMappedAndPrefixedRoles()
    {
        var store = Substitute.For<IOidcAccountStore>();
        var users = Users();
        var user = User;
        users.FindByEmailAsync("user@example.test").Returns(user);
        Assert.Same(user, await Service(store, users).Resolve(Identity, default));
        await store.Received().Link(user.Id, Identity.ProviderId, Identity.Issuer, Identity.Subject, default);
        await store.Received().SynchronizeRoles(user.Id, "company",
            Arg.Is<IReadOnlyList<string>>(roles => roles.SequenceEqual(new[] { "Editor" })),
            Arg.Is<IReadOnlyList<string>>(roles => roles.SequenceEqual(new[] { "oidc:company:reader" })), default);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task LinkingRequiresProviderOptInAndBothAddressesVerified(bool optIn, bool externalVerified, bool localConfirmed)
    {
        var store = Substitute.For<IOidcAccountStore>();
        var users = Users();
        var user = User;
        user.Emails = [new Email("user@example.test", localConfirmed ? Now : null)];
        users.FindByEmailAsync("user@example.test").Returns(user);
        Assert.Null(await Service(store, users, Provider with { LinkByVerifiedEmail = optIn })
            .Resolve(Identity with { EmailVerified = externalVerified }, default));
        await store.DidNotReceiveWithAnyArgs().Link(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task BoundIdentityUsesSubjectNotChangedEmailAndDeletedUserCannotRelink()
    {
        var store = Substitute.For<IOidcAccountStore>();
        var users = Users();
        var user = User;
        store.FindUser("company", Identity.Issuer, Identity.Subject, default).Returns(user.Id);
        users.FindByIdAsync(user.Id.ToString()).Returns(user);
        Assert.Same(user, await Service(store, users).Resolve(Identity with { Email = "changed@example.test" }, default));
        await users.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        users.FindByIdAsync(user.Id.ToString()).Returns((ApplicationUser?)null);
        Assert.Null(await Service(store, users).Resolve(Identity, default));
        await users.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AccountCreationRequiresProviderAndGlobalSignUp(bool autoCreate, bool signUp)
    {
        var store = Substitute.For<IOidcAccountStore>();
        var users = Users();
        Assert.Null(await Service(store, users, Provider with { AutoCreateUsers = autoCreate },
            new() { SignUp = signUp }).Resolve(Identity, default));
        await users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task NewOidcAccountHasVerifiedEmailAndRetainsModeratedApproval()
    {
        var store = Substitute.For<IOidcAccountStore>();
        var users = Users();
        var access = Substitute.For<IAccessSettingsQueries>();
        access.Current(default).Returns(AccessSettings.Open with { ModeratedSignUp = true });
        users.CreateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
        var created = await Service(store, users, access: access).Resolve(Identity, default);
        Assert.NotNull(created);
        Assert.True(created.PendingApproval);
        Assert.Equal(Now, Assert.Single(created.Emails).ConfirmedAt);
        Assert.Equal("User", created.DisplayName);
        await store.Received().Link(created.Id, "company", Identity.Issuer, Identity.Subject, default);
    }

    [Fact]
    public async Task CreationFailureNeverLinksOrSynchronizesRoles()
    {
        var store = Substitute.For<IOidcAccountStore>();
        var users = Users();
        var access = Substitute.For<IAccessSettingsQueries>();
        access.Current(default).Returns(AccessSettings.Open);
        users.CreateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Failed(new IdentityError { Code = "EmailAlreadyExists" }));
        Assert.Null(await Service(store, users, access: access).Resolve(Identity, default));
        await store.DidNotReceiveWithAnyArgs().Link(default, default!, default!, default!, default);
        await store.DidNotReceiveWithAnyArgs().SynchronizeRoles(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task RedeemUsesExistingBanAndTwoFactorGates()
    {
        var catalog = new OidcProviderCatalog(new() { Providers = [Provider] });
        var tickets = new OidcTicketStore(catalog, new FixedTimeProvider(Now));
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{OidcExtensions.CookieName("flow")}=proof";
        var signIn = Substitute.For<IOidcSignInService>();
        var user = User;
        signIn.Resolve(Arg.Is<OidcIdentity>(identity => identity.ProviderId == "company"
            && identity.Subject == "subject"), default).Returns(user);
        var sessions = Substitute.For<ISessionCommands>();
        var bans = Substitute.For<IWallOfShameQueries>();
        var secondFactor = Substitute.For<ITwoFactorGate>();
        secondFactor.ChallengeOidc(user.Id, "company", default).Returns(Some(
            new TwoFactorChallengeResponse("challenge", [TwoFactorMethod.Totp], false)));
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));

        async Task<IResult> Redeem() => await OidcSignInEndpoints.Redeem(
            new(tickets.Issue(Identity, "flow", "proof")), tickets, signIn, Options.Create(new AccountsConfiguration()),
            security, sessions, bans, secondFactor, new FixedTimeProvider(Now), context,
            NullLogger<OidcSignInEndpoints>.Instance);

        Assert.IsType<JsonHttpResult<TwoFactorChallengeResponse>>(await Redeem());
        await sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
        bans.FindActiveBan(user.Id, Now, default).Returns(Some(new IWallOfShameQueries.ActiveBan("blocked", None)));
        Assert.Equal(403, Assert.IsType<JsonHttpResult<SignInEndpoints.BannedResponse>>(await Redeem()).StatusCode);
        await sessions.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
        bans.FindActiveBan(user.Id, Now, default).Returns(None);
        secondFactor.ChallengeOidc(user.Id, "company", default).Returns(None);
        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Redeem());
        await sessions.Received(1).CreateForUserId(user.Id, Arg.Any<ValidSession>(), default);
    }

    [Fact]
    public void ProviderListingHonorsGlobalSwitchAndChallengeRequiresHttps()
    {
        var catalog = new OidcProviderCatalog(new() { Providers = [Provider] });
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var accounts = Options.Create(new AccountsConfiguration());
        var context = new DefaultHttpContext();
        var listing = Assert.IsType<JsonHttpResult<OidcSignInEndpoints.ProviderResponse[]>>(
            OidcSignInEndpoints.Providers(catalog, accounts, security, context));
        Assert.Equal("Company", Assert.Single(listing.Value!).DisplayName);
        Assert.IsType<BadRequest>(OidcSignInEndpoints.Challenge("company", catalog, accounts, security,
            context, NullLogger<OidcSignInEndpoints>.Instance));
        Assert.IsType<NotFound>(OidcSignInEndpoints.Challenge("unknown", catalog, accounts, security,
            context, NullLogger<OidcSignInEndpoints>.Instance));
        Assert.Empty(Assert.IsType<JsonHttpResult<OidcSignInEndpoints.ProviderResponse[]>>(
            OidcSignInEndpoints.Providers(catalog, Options.Create(new AccountsConfiguration { SignIn = false }),
                security, context)).Value!);
    }
}

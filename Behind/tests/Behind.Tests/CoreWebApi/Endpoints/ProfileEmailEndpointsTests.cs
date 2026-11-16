using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class ProfileEmailEndpointsTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly IEmailQueries.Address Address = new(Guid.NewGuid(), "new@test.com", null);
    private readonly IEmailCommands _commands = Substitute.For<IEmailCommands>();
    private readonly IEmailQueries _emails = Substitute.For<IEmailQueries>();
    private readonly IEmailSender<ApplicationUser> _sender = Substitute.For<IEmailSender<ApplicationUser>>();
    private readonly ApplicationUser _user = new() { Id = UserId, Emails = [new Email("old@test.com")] };
    private readonly UserManager<ApplicationUser> _manager;
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:Url"] = "https://app.test/" }).Build();

    public ProfileEmailEndpointsTests()
    {
        _manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        _manager.Options = new IdentityOptions();
        _manager.FindByIdAsync(UserId.ToString()).Returns(_user);
        _manager.GenerateUserTokenAsync(_user, Arg.Any<string>(), Arg.Any<string>()).Returns("token");
        _emails.Addresses(UserId, Arg.Any<CancellationToken>()).Returns(new[] { Address });
        _commands.SaveAddress(UserId, Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new IEmailCommands.AddressResult(IEmailCommands.AddressStatus.Saved, Address));
    }

    private static HttpContext SignedIn()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", SessionId.ToString())], "test"));
        return context;
    }

    private static ISessionQueries Sessions(bool valid = true, bool impersonated = false)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>()).Returns(valid
            ? Some<(Session, Guid)>((new ValidSession(
                AccessToken.Create(new GuestUser(None)), RefreshToken.Create(new GuestUser(None)), "127.0.0.1", "test")
            {
                Impersonator = impersonated ? Some<User>(new GuestUser(None)) : None
            }, UserId))
            : None);
        return queries;
    }

    private Task<IResult> Save(bool required = true, Guid? id = null, ISessionQueries? sessions = null) =>
        ProfileEmailEndpoints.Save(id, new("new@test.com"), SignedIn(), sessions ?? Sessions(), _commands, _manager,
            Options.Create(new SecurityConfiguration { EmailConfirmation = required }), _sender, _configuration,
            NullLogger<ProfileEmailEndpoints>.Instance);

    private Task<IResult> Resend(bool required = true) =>
        ProfileEmailEndpoints.Resend(Address.Id, SignedIn(), Sessions(), _emails, _manager,
            Options.Create(new SecurityConfiguration { EmailConfirmation = required }), _sender, _configuration,
            NullLogger<ProfileEmailEndpoints>.Instance);

    [Fact]
    public async Task Get_ReturnsPerAddressStatusAndConfiguration()
    {
        var result = await ProfileEmailEndpoints.Get(SignedIn(), Sessions(), _emails, _manager,
            Options.Create(new SecurityConfiguration()), NullLogger<ProfileEmailEndpoints>.Instance);
        var response = Assert.IsType<JsonHttpResult<ProfileEmailEndpoints.AddressesResponse>>(result).Value!;
        Assert.Equal(Address, Assert.Single(response.Items));
        Assert.True(response.ConfirmationRequired);
    }

    [Fact]
    public async Task Add_RequiresConfirmationAndSendsAnAddressBoundLink()
    {
        var response = Assert.IsType<Created<ProfileEmailEndpoints.SavedAddress>>(await Save()).Value!;
        Assert.True(response.ConfirmationRequired);
        Assert.True(response.EmailSent);
        await _commands.Received(1).SaveAddress(UserId, null, "new@test.com", true, Arg.Any<CancellationToken>());
        await _manager.Received(1).GenerateUserTokenAsync(_user,
            _manager.Options.Tokens.EmailConfirmationTokenProvider, Address.ConfirmationPurpose);
        await _sender.Received(1).SendConfirmationLinkAsync(_user, Address.Value,
            $"https://app.test/#/sign/up/confirm?userId={UserId}&token=token&emailId={Address.Id}");
    }

    [Fact]
    public async Task Edit_WithConfirmationDisabled_DoesNotSendEmail()
    {
        var response = Assert.IsType<Ok<ProfileEmailEndpoints.SavedAddress>>(await Save(false, Address.Id)).Value!;
        Assert.False(response.ConfirmationRequired);
        Assert.False(response.EmailSent);
        await _commands.Received(1).SaveAddress(UserId, Address.Id, "new@test.com", false, Arg.Any<CancellationToken>());
        await _sender.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

    [Fact]
    public async Task MailFailure_ExplicitlyReportsThatAddressWasSavedButEmailWasNotSent()
    {
        _sender.SendConfirmationLinkAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("mail unavailable")));
        var response = Assert.IsType<Created<ProfileEmailEndpoints.SavedAddress>>(await Save()).Value!;
        Assert.True(response.ConfirmationRequired);
        Assert.False(response.EmailSent);
        Assert.Equal(Address, response.Address);
        Assert.Equal(503, Assert.IsType<StatusCodeHttpResult>(await Resend()).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/relative")]
    [InlineData("javascript:alert(1)")]
    public async Task InvalidFrontendUrl_NeverGeneratesOrSendsConfirmationToken(string? url)
    {
        _configuration["Frontend:Url"] = url;

        var response = Assert.IsType<Created<ProfileEmailEndpoints.SavedAddress>>(await Save()).Value!;
        Assert.True(response.ConfirmationRequired);
        Assert.False(response.EmailSent);
        Assert.Equal(503, Assert.IsType<StatusCodeHttpResult>(await Resend()).StatusCode);
        await _manager.DidNotReceiveWithAnyArgs().GenerateUserTokenAsync(default!, default!, default!);
        await _sender.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task InvalidOrImpersonatedSession_CannotManageEmails(bool valid, bool impersonated)
    {
        var result = await Save(sessions: Sessions(valid, impersonated));
        Assert.IsType(valid ? typeof(ForbidHttpResult) : typeof(UnauthorizedHttpResult), result);
        await _commands.DidNotReceiveWithAnyArgs().SaveAddress(default, default, default!, default, default);
        await _sender.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

    [Theory]
    [InlineData(IEmailCommands.AddressStatus.InUse, typeof(Conflict<ProfileEmailEndpoints.Failure>))]
    [InlineData(IEmailCommands.AddressStatus.NotFound, typeof(NotFound))]
    public async Task Save_MapsDatabaseRejectionsWithoutSendingMail(IEmailCommands.AddressStatus status, Type expected)
    {
        _commands.SaveAddress(UserId, Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new IEmailCommands.AddressResult(status));
        Assert.IsType(expected, await Save(id: Address.Id));
        await _sender.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

    [Theory]
    [InlineData(IEmailCommands.AddressStatus.Saved, typeof(NoContent))]
    [InlineData(IEmailCommands.AddressStatus.NotFound, typeof(NotFound))]
    [InlineData(IEmailCommands.AddressStatus.LastAddress, typeof(Conflict<ProfileEmailEndpoints.Failure>))]
    public async Task Remove_UsesOwnershipAndLastAddressGuard(IEmailCommands.AddressStatus status, Type expected)
    {
        _commands.RemoveAddress(UserId, Address.Id, Arg.Any<CancellationToken>()).Returns(status);
        var result = await ProfileEmailEndpoints.Remove(Address.Id, SignedIn(), Sessions(), _commands, _manager,
            NullLogger<ProfileEmailEndpoints>.Instance);
        Assert.IsType(expected, result);
        await _commands.Received(1).RemoveAddress(UserId, Address.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resend_RequiresOwnedUnconfirmedAddressAndEnabledConfirmation()
    {
        Assert.IsType<NotFound>(await Resend(false));
        _emails.Addresses(UserId, Arg.Any<CancellationToken>()).Returns(Array.Empty<IEmailQueries.Address>());
        Assert.IsType<NotFound>(await Resend());
        _emails.Addresses(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { Address with { ConfirmedAt = DateTimeOffset.UtcNow } });
        Assert.IsType<NoContent>(await Resend());
        await _sender.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

    [Theory]
    [InlineData(nameof(ProfileEmailEndpoints.Get))]
    [InlineData(nameof(ProfileEmailEndpoints.Save))]
    [InlineData(nameof(ProfileEmailEndpoints.Remove))]
    [InlineData(nameof(ProfileEmailEndpoints.Resend))]
    public void ManagementEndpoints_RequireAuthorizationAndBypassETags(string name)
    {
        var method = typeof(ProfileEmailEndpoints).GetMethod(name)!;
        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(method.GetCustomAttribute<IgnoreETagAttribute>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("a@test.com\nb@test.com")]
    public void AddressRequest_RejectsInvalidEmails(string value)
    {
        var request = new ProfileEmailEndpoints.AddressRequest(value);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Theory]
    [InlineData(247, false)]
    [InlineData(246, true)]
    public void AddressRequest_EnforcesThe255CharacterStorageLimit(int localLength, bool valid)
    {
        var request = new ProfileEmailEndpoints.AddressRequest(new string('a', localLength) + "@test.com");
        Assert.Equal(valid, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Fact]
    public async Task RealConfirmationToken_IsBoundToUserAddressIdAndValue()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        store.GetUserIdAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ApplicationUser>().Id.ToString());
        using var manager = new UserManager<ApplicationUser>(store, Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), Substitute.For<IServiceProvider>(), NullLogger<UserManager<ApplicationUser>>.Instance);
        manager.RegisterTokenProvider(manager.Options.Tokens.EmailConfirmationTokenProvider,
            new DataProtectorTokenProvider<ApplicationUser>(new EphemeralDataProtectionProvider(),
                Options.Create(new DataProtectionTokenProviderOptions()), NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance));
        var provider = manager.Options.Tokens.EmailConfirmationTokenProvider;
        var token = await manager.GenerateUserTokenAsync(_user, provider, Address.ConfirmationPurpose);
        Assert.True(await manager.VerifyUserTokenAsync(_user, provider, Address.ConfirmationPurpose, token));
        Assert.False(await manager.VerifyUserTokenAsync(_user, provider,
            (Address with { Value = "replacement@test.com" }).ConfirmationPurpose, token));
        Assert.False(await manager.VerifyUserTokenAsync(_user, provider,
            (Address with { Id = Guid.NewGuid() }).ConfirmationPurpose, token));
        Assert.False(await manager.VerifyUserTokenAsync(new ApplicationUser(), provider, Address.ConfirmationPurpose, token));
    }
}

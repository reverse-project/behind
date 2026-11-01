using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SignUpEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IEmailSender<ApplicationUser> _emailSender = Substitute.For<IEmailSender<ApplicationUser>>();

    private static IOptions<SecurityConfiguration> Security(bool emailConfirmation) =>
        Options.Create(new SecurityConfiguration { EmailConfirmation = emailConfirmation });

    private static IConfiguration FrontendConfigurationOf(string? url) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(url is null ? [] : new Dictionary<string, string?> { ["Frontend:Url"] = url })
            .Build();

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.test");
        return context;
    }

    private Task<IResult> SignUp(
        UserManager<ApplicationUser> manager,
        SignUpEndpoints.PasswordRequest request,
        ILogger<SignUpEndpoints>? logger = null,
        bool emailConfirmation = true,
        string? frontendUrl = null) =>
        SignUpEndpoints.SignUpPassword(
            manager,
            Security(emailConfirmation),
            _emailSender,
            FrontendConfigurationOf(frontendUrl),
            new FixedTimeProvider(Now),
            NewHttpContext(),
            logger ?? NullLogger<SignUpEndpoints>.Instance,
            request);

    private static UserManager<ApplicationUser> ManagerCapturing(Action<ApplicationUser> capture)
    {
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);
        manager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        manager.GenerateEmailConfirmationTokenAsync(Arg.Any<ApplicationUser>()).Returns("the token");
        manager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(call =>
            {
                capture(call.Arg<ApplicationUser>());
                return IdentityResult.Success;
            });
        return manager;
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Ada  ", "Ada")]
    public async Task SignUpPassword_StoresNormalizedDisplayName(string? displayName, string expected)
    {
        ApplicationUser? created = null;
        var manager = ManagerCapturing(user => created = user);

        var result = await SignUp(manager, new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!", displayName));

        Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(expected, created!.DisplayName);
        Assert.Equal("ada@test.com", Assert.Single(created.Emails).Value);
    }

    [Fact]
    public async Task SignUpPassword_Success_LogsInformationWithUserId()
    {
        ApplicationUser? created = null;
        var logger = new CapturingLogger<SignUpEndpoints>();

        await SignUp(ManagerCapturing(user => created = user), new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"), logger);

        Assert.Equal(created!.Id, logger.Entries.First(entry => entry.Level == LogLevel.Information).State["UserId"]);
    }

    [Fact]
    public async Task SignUpPassword_EmailInUse_LogsWarning()
    {
        var manager = ManagerCapturing(_ => { });
        manager.FindByEmailAsync(Arg.Any<string>()).Returns(new ApplicationUser());
        var logger = new CapturingLogger<SignUpEndpoints>();

        await SignUp(manager, new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"), logger);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task SignUpPassword_IdentityFailure_LogsWarningWithErrorCodes()
    {
        var manager = ManagerCapturing(_ => { });
        manager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "short" }));
        var logger = new CapturingLogger<SignUpEndpoints>();

        await SignUp(manager, new SignUpEndpoints.PasswordRequest("ada@test.com", "x"), logger);

        Assert.Equal(["PasswordTooShort"], (string[])logger.Single(LogLevel.Warning).State["ErrorCodes"]!);
    }

    [Fact]
    public async Task SignUpPassword_WithoutDisplayName_IsStillValid()
    {
        ApplicationUser? created = null;
        var manager = ManagerCapturing(user => created = user);

        await SignUp(manager, new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"));

        Assert.Equal(string.Empty, created!.DisplayName);
    }

    [Fact]
    public async Task SignUpPassword_ConfirmationEnabled_LeavesEmailUnconfirmedAndSendsLink()
    {
        ApplicationUser? created = null;

        await SignUp(
            ManagerCapturing(user => created = user),
            new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"),
            frontendUrl: "https://app.test/");

        Assert.Null(Assert.Single(created!.Emails).ConfirmedAt);
        await _emailSender.Received(1).SendConfirmationLinkAsync(
            created,
            "ada@test.com",
            $"https://app.test/#/sign/up/confirm?userId={created.Id}&token=the%20token");
    }

    [Fact]
    public async Task SignUpPassword_ConfirmationEnabledWithoutFrontendUrl_UsesRequestBaseUri()
    {
        await SignUp(ManagerCapturing(_ => { }), new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"));

        await _emailSender.Received(1).SendConfirmationLinkAsync(
            Arg.Any<ApplicationUser>(),
            "ada@test.com",
            Arg.Is<string>(link => link.StartsWith("https://api.test/#/sign/up/confirm?")));
    }

    [Fact]
    public async Task SignUpPassword_ConfirmationEnabled_LogsSentConfirmation()
    {
        var logger = new CapturingLogger<SignUpEndpoints>();

        await SignUp(ManagerCapturing(_ => { }), new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"), logger);

        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("Confirmation email sent"));
    }

    [Fact]
    public async Task SignUpPassword_ConfirmationDisabled_ConfirmsEmailNowAndSendsNothing()
    {
        ApplicationUser? created = null;
        var logger = new CapturingLogger<SignUpEndpoints>();

        var result = await SignUp(
            ManagerCapturing(user => created = user),
            new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"),
            logger,
            emailConfirmation: false);

        Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(Now, Assert.Single(created!.Emails).ConfirmedAt);
        await _emailSender.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("email confirmation is disabled"));
    }

    [Fact]
    public async Task SignUpPassword_ConfirmationEmailFails_StillSucceedsAndLogsError()
    {
        var logger = new CapturingLogger<SignUpEndpoints>();
        _emailSender.SendConfirmationLinkAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("smtp down")));

        var result = await SignUp(
            ManagerCapturing(_ => { }),
            new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"),
            logger);

        Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal("smtp down", logger.Single(LogLevel.Error).Exception!.Message);
    }

    [Fact]
    public async Task SignUpPassword_IdentityFailure_SendsNoConfirmation()
    {
        var manager = ManagerCapturing(_ => { });
        manager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));

        await SignUp(manager, new SignUpEndpoints.PasswordRequest("ada@test.com", "x"));

        await _emailSender.DidNotReceiveWithAnyArgs().SendConfirmationLinkAsync(default!, default!, default!);
    }

    private static UserManager<ApplicationUser> ManagerConfirming(ApplicationUser? user, IdentityResult result)
    {
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(Arg.Any<string>()).Returns(user);
        manager.ConfirmEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(result);
        return manager;
    }

    [Fact]
    public async Task ConfirmEmail_ValidToken_ReturnsNoContentAndLogsInformation()
    {
        var user = new ApplicationUser { Emails = [new Email("ada@test.com")] };
        var manager = ManagerConfirming(user, IdentityResult.Success);
        var logger = new CapturingLogger<SignUpEndpoints>();

        var result = await SignUpEndpoints.ConfirmEmail(
            manager,
            logger,
            new SignUpEndpoints.ConfirmEmailRequest(user.Id, "token"));

        Assert.IsType<NoContent>(result);
        await manager.Received(1).ConfirmEmailAsync(user, "token");
        Assert.Equal(user.Id, logger.Single(LogLevel.Information).State["UserId"]);
    }

    [Fact]
    public async Task ConfirmEmail_UnknownUser_ReturnsBadRequestAndLogsWarning()
    {
        var logger = new CapturingLogger<SignUpEndpoints>();

        var result = await SignUpEndpoints.ConfirmEmail(
            ManagerConfirming(null, IdentityResult.Success),
            logger,
            new SignUpEndpoints.ConfirmEmailRequest(Guid.NewGuid(), "token"));

        Assert.IsType<BadRequest>(result);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_ReturnsBadRequestAndLogsErrorCodes()
    {
        var user = new ApplicationUser { Emails = [new Email("ada@test.com")] };
        var manager = ManagerConfirming(user, IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));
        var logger = new CapturingLogger<SignUpEndpoints>();

        var result = await SignUpEndpoints.ConfirmEmail(
            manager,
            logger,
            new SignUpEndpoints.ConfirmEmailRequest(user.Id, "bad"));

        Assert.IsType<BadRequest>(result);
        Assert.Equal(["InvalidToken"], (string[])logger.Single(LogLevel.Warning).State["ErrorCodes"]!);
    }
}

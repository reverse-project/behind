using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SignUpEndpointsTests
{
    private static UserManager<ApplicationUser> ManagerCapturing(Action<ApplicationUser> capture)
    {
        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null, null, null, null, null, null, null, null);
        manager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
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

        var result = await SignUpEndpoints.SignUpPassword(
            manager,
            NullLogger<SignUpEndpoints>.Instance,
            new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!", displayName));

        Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(expected, created!.DisplayName);
        Assert.Equal("ada@test.com", Assert.Single(created.Emails).Value);
    }

    [Fact]
    public async Task SignUpPassword_Success_LogsInformationWithUserId()
    {
        ApplicationUser? created = null;
        var logger = new CapturingLogger<SignUpEndpoints>();

        await SignUpEndpoints.SignUpPassword(
            ManagerCapturing(user => created = user),
            logger,
            new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"));

        Assert.Equal(created!.Id, logger.Single(LogLevel.Information).State["UserId"]);
    }

    [Fact]
    public async Task SignUpPassword_EmailInUse_LogsWarning()
    {
        var manager = ManagerCapturing(_ => { });
        manager.FindByEmailAsync(Arg.Any<string>()).Returns(new ApplicationUser());
        var logger = new CapturingLogger<SignUpEndpoints>();

        await SignUpEndpoints.SignUpPassword(
            manager,
            logger,
            new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"));

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task SignUpPassword_IdentityFailure_LogsWarningWithErrorCodes()
    {
        var manager = ManagerCapturing(_ => { });
        manager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "short" }));
        var logger = new CapturingLogger<SignUpEndpoints>();

        await SignUpEndpoints.SignUpPassword(
            manager,
            logger,
            new SignUpEndpoints.PasswordRequest("ada@test.com", "x"));

        Assert.Equal(["PasswordTooShort"], (string[])logger.Single(LogLevel.Warning).State["ErrorCodes"]!);
    }

    [Fact]
    public async Task SignUpPassword_WithoutDisplayName_IsStillValid()
    {
        ApplicationUser? created = null;
        var manager = ManagerCapturing(user => created = user);

        await SignUpEndpoints.SignUpPassword(
            manager,
            NullLogger<SignUpEndpoints>.Instance,
            new SignUpEndpoints.PasswordRequest("ada@test.com", "Passw0rd!"));

        Assert.Equal(string.Empty, created!.DisplayName);
    }
}

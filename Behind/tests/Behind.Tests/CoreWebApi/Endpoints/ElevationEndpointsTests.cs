using System.Security.Claims;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ElevationEndpointsTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly FixedTimeProvider Time = new(Now);
    private static readonly byte[] AdministratorKey = Enumerable.Range(1, 20).Select(i => (byte)i).ToArray();
    private static readonly byte[] DeveloperKey = Enumerable.Range(101, 20).Select(i => (byte)i).ToArray();

    private static string CodeFor(byte[] key) => new OtpNet.Totp(key, mode: Totp.HashMode).ComputeTotp(Now.UtcDateTime);

    private static Option<SecretKey> KeyOf(byte[] key)
    {
        Totp.TryCreate(OtpNet.Base32Encoding.ToString(key), out var totp);
        return Some<SecretKey>(totp);
    }

    private static IOptions<SecurityConfiguration> Security(
        bool enabled = true,
        Option<SecretKey>? administrator = null,
        Option<SecretKey>? developer = null) =>
        Options.Create(new SecurityConfiguration(
            false,
            administrator ?? KeyOf(AdministratorKey),
            None,
            None,
            None,
            Some(new ElevationSettings(enabled, developer ?? KeyOf(DeveloperKey)))));

    private static DefaultHttpContext SignedIn()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("session_id", SessionId.ToString())],
            "test"));
        return context;
    }

    private static ISessionQueries ValidSessionQueries()
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((
                new ValidSession(
                    AccessToken.Create(new GuestUser(None)),
                    RefreshToken.Create(new GuestUser(None)),
                    "10.0.0.1",
                    "test-agent"),
                UserId)));
        return queries;
    }

    private static Task<IResult> Administrator(
        string code,
        IOptions<SecurityConfiguration> security,
        IRoleCommands roles,
        HttpContext? context = null,
        ISessionQueries? queries = null) =>
        ElevationEndpoints.ElevateAdministrator(
            new ElevationEndpoints.ElevationRequest(code),
            context ?? SignedIn(),
            security,
            queries ?? ValidSessionQueries(),
            roles,
            Time,
            NullLogger<ElevationEndpoints>.Instance);

    private static Task<IResult> Developer(
        string code,
        IOptions<SecurityConfiguration> security,
        IRoleCommands roles,
        HttpContext? context = null,
        ISessionQueries? queries = null) =>
        ElevationEndpoints.ElevateDeveloper(
            new ElevationEndpoints.ElevationRequest(code),
            context ?? SignedIn(),
            security,
            queries ?? ValidSessionQueries(),
            roles,
            Time,
            NullLogger<ElevationEndpoints>.Instance);

    private static async Task AssertNothingAssigned(IRoleCommands roles) =>
        await roles.DidNotReceiveWithAnyArgs().AssignBuiltIn(default, default!, default);

    private static void AssertForbidden(IResult result) =>
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);

    private static async Task<CapturingLogger<ElevationEndpoints>> LoggedAdministrator(
        string code,
        IOptions<SecurityConfiguration> security,
        HttpContext? context = null,
        ISessionQueries? queries = null)
    {
        var logger = new CapturingLogger<ElevationEndpoints>();

        await ElevationEndpoints.ElevateAdministrator(
            new ElevationEndpoints.ElevationRequest(code),
            context ?? SignedIn(),
            security,
            queries ?? ValidSessionQueries(),
            Substitute.For<IRoleCommands>(),
            Time,
            logger);

        return logger;
    }

    [Fact]
    public async Task ElevateAdministrator_ValidCode_LogsInformationWithUserAndRole()
    {
        var logger = await LoggedAdministrator(CodeFor(AdministratorKey), Security());

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal(UserId, entry.State["UserId"]);
        Assert.Equal(BuiltInAccess.Administrators.Role.Name, entry.State["Role"]);
    }

    [Fact]
    public async Task ElevateAdministrator_InvalidCode_LogsWarningWithUser()
    {
        var logger = await LoggedAdministrator(CodeFor(DeveloperKey), Security());

        Assert.Equal(UserId, logger.Single(LogLevel.Warning).State["UserId"]);
        Assert.False(logger.Has(LogLevel.Information));
    }

    [Fact]
    public async Task ElevateAdministrator_ElevationDisabled_LogsWarning()
    {
        var logger = await LoggedAdministrator(CodeFor(AdministratorKey), Security(enabled: false));

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task ElevateAdministrator_WithoutSession_LogsWarning()
    {
        var logger = await LoggedAdministrator(
            CodeFor(AdministratorKey),
            Security(),
            new DefaultHttpContext());

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task ElevateAdministrator_ValidCode_AssignsAdministratorsRole()
    {
        var roles = Substitute.For<IRoleCommands>();

        var result = await Administrator(CodeFor(AdministratorKey), Security(), roles);

        Assert.IsType<NoContent>(result);
        await roles.Received(1).AssignBuiltIn(UserId, BuiltInAccess.Administrators, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElevateDeveloper_ValidCode_AssignsDevelopersRole()
    {
        var roles = Substitute.For<IRoleCommands>();

        var result = await Developer(CodeFor(DeveloperKey), Security(), roles);

        Assert.IsType<NoContent>(result);
        await roles.Received(1).AssignBuiltIn(UserId, BuiltInAccess.Developers, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElevateDeveloper_AdministratorCode_ReturnsForbidden()
    {
        var roles = Substitute.For<IRoleCommands>();

        AssertForbidden(await Developer(CodeFor(AdministratorKey), Security(), roles));
        await AssertNothingAssigned(roles);
    }

    [Fact]
    public async Task ElevateAdministrator_DeveloperCode_ReturnsForbidden()
    {
        var roles = Substitute.For<IRoleCommands>();

        AssertForbidden(await Administrator(CodeFor(DeveloperKey), Security(), roles));
        await AssertNothingAssigned(roles);
    }

    [Fact]
    public async Task ElevateDeveloper_WithoutDeveloperSecretKey_ReturnsForbidden()
    {
        var roles = Substitute.For<IRoleCommands>();
        var security = Security(developer: None);

        AssertForbidden(await Developer(CodeFor(DeveloperKey), security, roles));
        await AssertNothingAssigned(roles);
    }

    [Fact]
    public async Task ElevateAdministrator_WithoutSecretKey_ReturnsForbidden()
    {
        var roles = Substitute.For<IRoleCommands>();
        var security = Security(administrator: None);

        AssertForbidden(await Administrator(CodeFor(AdministratorKey), security, roles));
        await AssertNothingAssigned(roles);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("not-a-code")]
    public async Task Elevate_WrongCode_ReturnsForbidden(string code)
    {
        var roles = Substitute.For<IRoleCommands>();

        AssertForbidden(await Administrator(code, Security(), roles));
        AssertForbidden(await Developer(code, Security(), roles));
        await AssertNothingAssigned(roles);
    }

    [Fact]
    public async Task Elevate_Disabled_ReturnsNotFoundForBothRoles()
    {
        var roles = Substitute.For<IRoleCommands>();
        var security = Security(enabled: false);

        Assert.IsType<NotFound>(await Administrator(CodeFor(AdministratorKey), security, roles));
        Assert.IsType<NotFound>(await Developer(CodeFor(DeveloperKey), security, roles));
        await AssertNothingAssigned(roles);
    }

    [Fact]
    public async Task Elevate_WithoutElevationSection_ReturnsNotFound()
    {
        var roles = Substitute.For<IRoleCommands>();
        var security = Options.Create(new SecurityConfiguration(false, KeyOf(AdministratorKey), None, None));

        Assert.IsType<NotFound>(await Administrator(CodeFor(AdministratorKey), security, roles));
        await AssertNothingAssigned(roles);
    }

    [Fact]
    public async Task Elevate_WithoutSession_ReturnsUnauthorized()
    {
        var roles = Substitute.For<IRoleCommands>();
        var anonymous = new DefaultHttpContext { User = new ClaimsPrincipal() };

        var result = await Developer(
            CodeFor(DeveloperKey),
            Security(),
            roles,
            anonymous,
            Substitute.For<ISessionQueries>());

        Assert.IsType<UnauthorizedHttpResult>(result);
        await AssertNothingAssigned(roles);
    }

    [Fact]
    public async Task Elevate_UnknownSession_ReturnsUnauthorized()
    {
        var roles = Substitute.For<IRoleCommands>();
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>()).Returns(None);

        var result = await Administrator(CodeFor(AdministratorKey), Security(), roles, queries: queries);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await AssertNothingAssigned(roles);
    }
}

using System.Reflection;
using System.Security.Claims;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class UserSettingEndpointsTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static DefaultHttpContext SignedIn() =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", SessionId.ToString())], "test"))
        };

    private static ISessionQueries SessionQueries(bool valid)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(SessionId, Arg.Any<CancellationToken>())
            .Returns(valid
                ? Some<(Session, Guid)>((
                    new ValidSession(
                        AccessToken.Create(new GuestUser(None)),
                        RefreshToken.Create(new GuestUser(None)),
                        "10.0.0.1",
                        "test-agent"),
                    UserId))
                : None);
        return queries;
    }

    private static Task<IResult> Set(
        string key,
        string? value,
        IUserSettingCommands commands,
        bool validSession = true) =>
        UserSettingEndpoints.Set(
            key,
            new UserSettingEndpoints.UserSettingRequest(value),
            SignedIn(),
            SessionQueries(validSession),
            commands,
            TestLocales.Embedded);

    [Fact]
    public async Task Get_ReturnsTheSettingsOfTheSessionUser()
    {
        var queries = Substitute.For<IUserSettingQueries>();
        queries.GetAll(UserId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { [UserSettingKeys.Locale] = "hu-HU" });

        var result = await UserSettingEndpoints.Get(SignedIn(), SessionQueries(true), queries);

        var settings = Assert.IsType<JsonHttpResult<IReadOnlyDictionary<string, string>>>(result).Value!;
        Assert.Equal("hu-HU", settings[UserSettingKeys.Locale]);
    }

    [Fact]
    public async Task Get_WithoutValidSession_ReturnsUnauthorized()
    {
        var result = await UserSettingEndpoints.Get(
            SignedIn(),
            SessionQueries(false),
            Substitute.For<IUserSettingQueries>());

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory]
    [InlineData("hu-HU")]
    [InlineData("en-US")]
    public async Task Set_AvailableLocale_StoresIt(string locale)
    {
        var commands = Substitute.For<IUserSettingCommands>();

        var result = await Set(UserSettingKeys.Locale, locale, commands);

        Assert.IsType<NoContent>(result);
        await commands.Received(1).Set(UserId, UserSettingKeys.Locale, locale, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Set_BlankLocale_StoresAnEmptyValueToUseTheSiteDefault(string? locale)
    {
        var commands = Substitute.For<IUserSettingCommands>();

        var result = await Set(UserSettingKeys.Locale, locale, commands);

        Assert.IsType<NoContent>(result);
        await commands.Received(1).Set(UserId, UserSettingKeys.Locale, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Set_TrimsTheValue()
    {
        var commands = Substitute.For<IUserSettingCommands>();

        await Set(UserSettingKeys.Locale, "  hu-HU  ", commands);

        await commands.Received(1).Set(UserId, UserSettingKeys.Locale, "hu-HU", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("../hu-HU")]
    public async Task Set_UnavailableLocale_IsABadRequest(string locale)
    {
        var commands = Substitute.For<IUserSettingCommands>();

        var result = await Set(UserSettingKeys.Locale, locale, commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Set(default, default!, default!, default);
    }

    [Fact]
    public async Task Set_UnknownKey_IsNotFound()
    {
        var commands = Substitute.For<IUserSettingCommands>();

        var result = await Set("unknown", "x", commands);

        Assert.IsType<NotFound>(result);
        await commands.DidNotReceiveWithAnyArgs().Set(default, default!, default!, default);
    }

    [Fact]
    public async Task Set_WithoutValidSession_ReturnsUnauthorized()
    {
        var commands = Substitute.For<IUserSettingCommands>();

        var result = await Set(UserSettingKeys.Locale, "hu-HU", commands, validSession: false);

        Assert.IsType<UnauthorizedHttpResult>(result);
        await commands.DidNotReceiveWithAnyArgs().Set(default, default!, default!, default);
    }

    [Theory]
    [InlineData(nameof(UserSettingEndpoints.Get))]
    [InlineData(nameof(UserSettingEndpoints.Set))]
    public void Endpoints_AreMarkedWithIgnoreETag(string name)
    {
        var method = typeof(UserSettingEndpoints).GetMethod(name);

        Assert.NotNull(method!.GetCustomAttribute<IgnoreETagAttribute>());
    }
}

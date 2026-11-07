using Behind.Core.Database;
using Behind.Core.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Middleware;

public class AccessGateMiddlewareTests
{
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IAccessSettingsQueries _settings = Substitute.For<IAccessSettingsQueries>();
    private readonly IAccessQueries _access = Substitute.For<IAccessQueries>();

    private AccessGate Gate() => new(_settings, SessionTestHelper.SessionQueries(SessionId, UserId), _access);

    private void Restrict() =>
        _settings.Current(Arg.Any<CancellationToken>()).Returns(new AccessSettings(true, "Users", "Members only", false));

    private static DefaultHttpContext Request(string path, DefaultHttpContext? context = null)
    {
        var request = context ?? new DefaultHttpContext();
        request.Request.Path = path;
        request.Response.Body = new MemoryStream();
        request.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        return request;
    }

    private async Task<bool> PassesAsync(DefaultHttpContext context)
    {
        var passed = false;
        var middleware = new AccessGateMiddleware(
            _ =>
            {
                passed = true;
                return Task.CompletedTask;
            },
            NullLogger<AccessGateMiddleware>.Instance);

        await middleware.InvokeAsync(context, Gate());

        return passed;
    }

    [Fact]
    public async Task InvokeAsync_RestrictionOff_PassesTheRequestOn()
    {
        _settings.Current(Arg.Any<CancellationToken>()).Returns(AccessSettings.Open);

        Assert.True(await PassesAsync(Request("/news")));
    }

    [Fact]
    public async Task InvokeAsync_RestrictedVisitor_GetsForbiddenWithTheRestrictedCode()
    {
        Restrict();
        var context = Request("/news");

        Assert.False(await PassesAsync(context));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        context.Response.Body.Position = 0;
        Assert.Contains($"\"code\":\"{AccessGateMiddleware.RestrictedCode}\"", new StreamReader(context.Response.Body).ReadToEnd());
    }

    [Theory]
    [InlineData("/sign/in/password")]
    [InlineData("/sign/up/password")]
    [InlineData("/sign/out")]
    [InlineData("/session")]
    [InlineData("/session/elevate/developer")]
    [InlineData("/accounts/settings")]
    [InlineData("/locales")]
    [InlineData("/locales/en-US.json")]
    [InlineData("/about")]
    [InlineData("/captcha")]
    [InlineData("/access/images/9d6b8b0c-2f4f-4d4b-8a52-0a0c4f1f3a11")]
    [InlineData("/profile/password")]
    [InlineData("/access/restriction")]
    public async Task InvokeAsync_RestrictedVisitor_CanStillReachTheOpenPaths(string path)
    {
        Restrict();

        Assert.True(await PassesAsync(Request(path)));
    }

    [Theory]
    [InlineData("/sessions")]
    [InlineData("/sites/index")]
    [InlineData("/news")]
    [InlineData("/articles/manage")]
    [InlineData("/access/manage")]
    public async Task InvokeAsync_RestrictedVisitor_IsKeptOutOfEverythingElse(string path)
    {
        Restrict();

        Assert.False(await PassesAsync(Request(path)));
    }

    [Fact]
    public async Task InvokeAsync_UserWithTheRole_PassesTheRequestOn()
    {
        Restrict();
        _access.GetUserAccess(UserId, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([new UserRole("Users")], [], []));

        Assert.True(await PassesAsync(Request("/news", SessionTestHelper.SignedIn(SessionId))));
    }
}

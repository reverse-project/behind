using System.Security.Claims;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Layouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class LayoutEndpointsTests
{
    private static LayoutConfiguration Configuration() => new()
    {
        Layouts = [new SiteLayout(), new SiteLayout { Id = "deleted", Name = "Deleted", Deleted = true }]
    };

    [Fact]
    public async Task PublicRead_ExcludesDeletedLayouts()
    {
        var service = Substitute.For<ILayoutService>();
        service.Read(Arg.Any<CancellationToken>()).Returns(Configuration());
        var result = await LayoutEndpoints.Get(service, CancellationToken.None);
        var value = Assert.IsType<LayoutConfiguration>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Single(value.Layouts);
        Assert.False(value.Layouts[0].Deleted);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task Manage_RequiresDeveloperGrantForDeletedLayouts(bool developer, int expected)
    {
        var context = new DefaultHttpContext();
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant)
            .Returns(developer ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        var service = Substitute.For<ILayoutService>();
        service.Read(Arg.Any<CancellationToken>()).Returns(Configuration());
        var result = await LayoutEndpoints.Manage(context, authorization, service, CancellationToken.None);
        var value = Assert.IsType<LayoutConfiguration>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(expected, value.Layouts.Count);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task ReadOnlyConfiguration_NeverWrites()
    {
        var service = Substitute.For<ILayoutService>();
        var result = await LayoutEndpoints.Save(
            new LayoutConfiguration(), new DefaultHttpContext(), Substitute.For<IAuthorizationService>(), service,
            Options.Create(new SecurityConfiguration { ReadOnlyConfiguration = true }),
            NullLogger<LayoutService>.Instance, CancellationToken.None);
        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        await service.DidNotReceive().Save(Arg.Any<LayoutConfiguration>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid", 400)]
    [InlineData("conflict", 409)]
    [InlineData("forbidden", 403)]
    public async Task FailedSave_UsesExplicitStatus(string error, int status)
    {
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant)
            .Returns(AuthorizationResult.Failed());
        var service = Substitute.For<ILayoutService>();
        service.Save(Arg.Any<LayoutConfiguration>(), false, Arg.Any<CancellationToken>())
            .Returns(new LayoutSaveResult(error, null));
        var result = await LayoutEndpoints.Save(
            new LayoutConfiguration(), new DefaultHttpContext(), authorization, service,
            Options.Create(new SecurityConfiguration()), NullLogger<LayoutService>.Instance, CancellationToken.None);
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }
}

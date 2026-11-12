using System.Security.Claims;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Layouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

    [Theory]
    [InlineData("GET", null)]
    [InlineData("POST", "Configuration.Manage")]
    public async Task ImageRoutes_RequireConfigurationManagementOnlyForUploads(string method, string? policy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped(_ => Substitute.For<ILayoutService>());
        builder.Services.AddScoped(_ => Substitute.For<IVfsCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IVfsQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IAuthorizationService>());
        await using var application = builder.Build();
        application.MapLayoutEndpoints();
        var pattern = method == "GET" ? "/layouts/images/{id:guid}" : "/layouts/images";
        var route = ((IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == pattern);
        Assert.Equal(policy, route.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
        Assert.Contains(method, route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }

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
            NullLogger<LayoutService>.Instance, Substitute.For<IVfsCommands>(), CancellationToken.None);
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
            Options.Create(new SecurityConfiguration()), NullLogger<LayoutService>.Instance, Substitute.For<IVfsCommands>(), CancellationToken.None);
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Save_AttachesCustomWidgetAndBadgeImagesToTheLayoutConfiguration()
    {
        var image = Guid.NewGuid();
        var unused = Guid.NewGuid();
        var badgeImage = Guid.NewGuid();
        var configuration = new LayoutConfiguration
        {
            Layouts = [new SiteLayout
            {
                Badges = [new LayoutBadge { Text = "Funding", Image = $"/layouts/images/{badgeImage}" }],
                Sections = [new LayoutSection
                {
                    Widgets =
                    [
                        new LayoutWidget { Type = "core.custom", Content = $"""<img src="/layouts/images/{image}">""" },
                        new LayoutWidget { Type = "core.content", Content = $"""<img src="/layouts/images/{unused}">""" }
                    ]
                }]
            }]
        };
        var service = Substitute.For<ILayoutService>();
        service.Save(configuration, false, Arg.Any<CancellationToken>()).Returns(new LayoutSaveResult(null, configuration));
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant)
            .Returns(AuthorizationResult.Failed());
        var files = Substitute.For<IVfsCommands>();

        await LayoutEndpoints.Save(
            configuration, new DefaultHttpContext(), authorization, service,
            Options.Create(new SecurityConfiguration()), NullLogger<LayoutService>.Instance, files, CancellationToken.None);

        await files.Received(1).Attach(VfsTypes.Layouts, LayoutConfiguration.ImagesKey,
            Arg.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 2 && keys.Contains(image) && keys.Contains(badgeImage)), Arg.Any<CancellationToken>());
    }
}

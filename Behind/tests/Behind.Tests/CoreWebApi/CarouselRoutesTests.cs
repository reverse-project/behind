using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public class CarouselRoutesTests
{
    private static WebApplication App()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.Configure<CarouselsConfiguration>(
            builder.Configuration.GetSection(CarouselsConfiguration.SectionName));
        builder.Services.AddAuthorization();
        builder.Services.AddScoped(_ => Substitute.For<ICarouselQueries>());
        builder.Services.AddScoped(_ => Substitute.For<ICarouselCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IVfsQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IVfsCommands>());

        var app = builder.Build();
        app.MapCarouselEndpoints();

        return app;
    }

    private static List<RouteEndpoint> Routes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

    private static RouteEndpoint Find(List<RouteEndpoint> routes, string method, string pattern) =>
        routes.Single(route =>
            route.RoutePattern.RawText == pattern
            && route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

    private static string? Policy(RouteEndpoint route) =>
        route.Metadata.GetOrderedMetadata<IAuthorizeData>().SingleOrDefault()?.Policy;

    [Theory]
    [InlineData("GET", "/carousels/settings")]
    [InlineData("GET", "/carousels/sites/{siteId:guid}")]
    [InlineData("GET", "/carousels/images/{id:guid}")]
    public void Reading_IsPublic(string method, string pattern)
    {
        Assert.Empty(Find(Routes(App()), method, pattern).Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    [Theory]
    [InlineData("GET", "/sites/{siteId:guid}/carousel", "Site.Read")]
    [InlineData("POST", "/sites/{siteId:guid}/carousel", "Site.Write")]
    [InlineData("POST", "/carousels/images", "Site.Write")]
    [InlineData("POST", "/sites/{siteId:guid}/carousel/delete", "Site.Delete")]
    [InlineData("POST", "/sites/{siteId:guid}/carousel/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/sites/{siteId:guid}/carousel/hard/delete", "RawDatabase.Manage")]
    public void Changes_RequireTheirGrant(string method, string pattern, string policy)
    {
        Assert.Equal(policy, Policy(Find(Routes(App()), method, pattern)));
    }
}

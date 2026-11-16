using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public class RouteAliasRoutesTests
{
    private static List<RouteEndpoint> Routes()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddScoped(_ => Substitute.For<IRouteAliasQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IRouteAliasCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IAuthorizationService>());

        var app = builder.Build();
        app.MapRouteAliasEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static RouteEndpoint Find(string method, string pattern) =>
        Routes().Single(route =>
            route.RoutePattern.RawText == pattern
            && route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

    private static string? Policy(RouteEndpoint route) =>
        route.Metadata.GetOrderedMetadata<IAuthorizeData>().SingleOrDefault()?.Policy;

    [Fact]
    public void Resolving_IsPublic()
    {
        Assert.Empty(Find("GET", "/route-aliases/resolve/{alias}").Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    [Theory]
    [InlineData("GET", "/route-aliases/manage", "RouteAlias.Read")]
    [InlineData("POST", "/route-aliases", "RouteAlias.Write")]
    [InlineData("POST", "/route-aliases/{id:guid}", "RouteAlias.Write")]
    [InlineData("POST", "/route-aliases/{id:guid}/delete", "RouteAlias.Delete")]
    [InlineData("POST", "/route-aliases/{id:guid}/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/route-aliases/{id:guid}/hard/delete", "RawDatabase.Manage")]
    public void Management_RequiresItsGrant(string method, string pattern, string policy)
    {
        Assert.Equal(policy, Policy(Find(method, pattern)));
    }
}

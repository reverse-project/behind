using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public class AccessRoutesTests
{
    private static List<RouteEndpoint> Routes()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddScoped(_ => Substitute.For<IAccessSettingsQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IAccessSettingsCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IRoleQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IUserApprovalQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IUserApprovalCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IUserCommands>());
        builder.Services.AddScoped(_ => Substitute.For<ISessionQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IAccessQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IUserManagementQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IVfsCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IVfsQueries>());
        builder.Services.AddScoped(_ => FixedOptionsSnapshot.Of(new SitesConfiguration()));
        builder.Services.AddScoped<AccessGate>();
        builder.Services.AddScoped<UserManagementGate>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped(_ => Substitute.For<IAuthorizationService>());

        var app = builder.Build();
        app.MapAccessEndpoints();

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
    public void RestrictionStatus_IsPublic()
    {
        Assert.Empty(Find("GET", "/access/restriction").Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    [Fact]
    public void ReadingImages_IsPublicBecauseTheRestrictionPageShowsThem()
    {
        Assert.Empty(Find("GET", "/access/images/{id:guid}").Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    [Fact]
    public void UploadingImages_NeedsAccessWrite()
    {
        Assert.Equal("Access.Write", Policy(Find("POST", "/access/images")));
    }

    [Theory]
    [InlineData("GET", "/access/manage", "Access.Read")]
    [InlineData("POST", "/access", "Access.Write")]
    [InlineData("POST", "/access/delete", "Access.Delete")]
    [InlineData("POST", "/access/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/access/hard/delete", "RawDatabase.Manage")]
    [InlineData("GET", "/access/pending-users", "Access.Read")]
    [InlineData("POST", "/access/pending-users/{id:guid}/approve", "Access.Write")]
    [InlineData("POST", "/access/pending-users/{id:guid}/reject", "Access.Write")]
    public void ManagementEndpoints_RequireTheirGrant(string method, string pattern, string policy)
    {
        Assert.Equal(policy, Policy(Find(method, pattern)));
    }
}

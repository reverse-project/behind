using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentAgeRoutesTests
{
    [Fact]
    public void AgeOptionsAndAssignmentsUseTheNewPoliciesWithoutLegacyManagementRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddOptions();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<AbstractCoreContext>(_ => null!);
        var app = builder.Build();
        app.MapContentAgeOptionEndpoints();
        app.MapContentRestrictionEndpoints();
        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToList();

        RouteEndpoint Find(string method, string pattern) => routes.Single(route =>
            route.RoutePattern.RawText == pattern
            && route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

        foreach (var (method, path) in new[]
                 {
                     ("POST", "/content-age-options"),
                     ("POST", "/content-age-options/{id:guid}/update"),
                     ("POST", "/content-age-options/{id:guid}/update"),
                     ("POST", "/content-age-options/{id:guid}/delete"),
                     ("POST", "/content-age-options/{id:guid}/restore"),
                     ("POST", "/content-age-options/{id:guid}/hard/delete")
                 })
        {
            var policies = Find(method, path).Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy).ToArray();
            Assert.Contains("ContentAge.Manage", policies);
            Assert.Equal(path.EndsWith("/restore") || path.EndsWith("/hard/delete"),
                policies.Contains("RawDatabase.Manage"));
        }

        Assert.NotEmpty(Find("GET", "/content-age-options").Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.NotEmpty(Find("GET", "/content-restrictions/{entityType}/{id:guid}").Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.NotEmpty(Find("POST", "/content-restrictions").Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.DoesNotContain(routes, route =>
            route.RoutePattern.RawText == "/content-restrictions"
            && route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET"));
        Assert.DoesNotContain(routes, route =>
            route.RoutePattern.RawText == "/content-restrictions/{id:guid}/delete");
        Assert.DoesNotContain(routes, route =>
            route.RoutePattern.RawText == "/content-restrictions/{id:guid}/restore");
    }
}

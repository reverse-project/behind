using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public class RawDatabaseRoutesTests
{
    private static List<RouteEndpoint> Routes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped(_ => Substitute.For<IRawDatabaseQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IRawDatabaseCommands>());

        var app = builder.Build();
        app.MapRawDatabaseEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    [Theory]
    [InlineData("GET", "/raw-database/tables")]
    [InlineData("GET", "/raw-database/tables/{table}/rows")]
    [InlineData("POST", "/raw-database/tables/{table}/rows")]
    [InlineData("POST", "/raw-database/tables/{table}/rows/update")]
    [InlineData("POST", "/raw-database/tables/{table}/rows/delete")]
    public void EveryRoute_RequiresTheRawDatabaseManageGrant(string method, string pattern)
    {
        var route = Routes().Single(candidate =>
            candidate.RoutePattern.RawText == pattern
            && candidate.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

        Assert.Equal(
            "RawDatabase.Manage",
            route.Metadata.GetOrderedMetadata<IAuthorizeData>().Single().Policy);
    }

    [Fact]
    public void Routes_AreExactlyTheFiveRawDatabaseOnes()
    {
        Assert.Equal(5, Routes().Count);
    }
}

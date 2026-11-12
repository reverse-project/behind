using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public sealed class MessageRoutesTests
{
    [Theory]
    [InlineData("GET", "/messages/manage", "Message.Read")]
    [InlineData("GET", "/messages/{id:guid}", "Message.Read")]
    [InlineData("POST", "/messages", "Message.Write")]
    [InlineData("POST", "/messages/{id:guid}", "Message.Write")]
    [InlineData("POST", "/messages/{id:guid}/delete", "Message.Delete")]
    [InlineData("POST", "/messages/{id:guid}/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/messages/{id:guid}/hard/delete", "RawDatabase.Manage")]
    public async Task Management_RequiresItsGrant(string method, string pattern, string policy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped(_ => Substitute.For<IMessageQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IMessageCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IAuthorizationService>());
        await using var app = builder.Build();
        app.MapMessageEndpoints();

        var route = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == pattern
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

        Assert.Equal(policy, Assert.Single(route.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
    }
}

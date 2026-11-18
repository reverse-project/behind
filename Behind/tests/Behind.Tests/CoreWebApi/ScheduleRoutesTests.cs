using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public sealed class ScheduleRoutesTests
{
    [Theory]
    [InlineData("GET", "/schedules/manage", "Schedule.Read")]
    [InlineData("GET", "/schedules/actions", "Schedule.Read")]
    [InlineData("GET", "/schedules/actions/{key}/options/{parameter}", "Schedule.Write")]
    [InlineData("GET", "/schedules/cron", "Schedule.Read")]
    [InlineData("GET", "/schedules/participants", "Schedule.Write")]
    [InlineData("GET", "/schedules/runs", "Schedule.Read")]
    [InlineData("POST", "/schedules/runs/{id:guid}/delete", "Schedule.Delete")]
    [InlineData("POST", "/schedules/runs/{id:guid}/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/schedules/runs/{id:guid}/hard/delete", "RawDatabase.Manage")]
    [InlineData("GET", "/schedules/{id:guid}", "Schedule.Read")]
    [InlineData("POST", "/schedules", "Schedule.Write")]
    [InlineData("POST", "/schedules/{id:guid}", "Schedule.Write")]
    [InlineData("POST", "/schedules/{id:guid}/run", "Schedule.Manage")]
    [InlineData("POST", "/schedules/{id:guid}/delete", "Schedule.Delete")]
    [InlineData("POST", "/schedules/{id:guid}/restore", "RawDatabase.Manage")]
    [InlineData("POST", "/schedules/{id:guid}/hard/delete", "RawDatabase.Manage")]
    public async Task Endpoints_RequireTheirGrant(string method, string pattern, string policy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped(_ => Substitute.For<IScheduleQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IScheduleCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IScheduleRunQueries>());
        builder.Services.AddScoped(_ => Substitute.For<IScheduleRunCommands>());
        builder.Services.AddScoped(_ => Substitute.For<IScheduleParticipantQueries>());
        builder.Services.AddSingleton(_ => Substitute.For<IScheduleRunner>());
        builder.Services.AddScoped(_ => Substitute.For<IAuthorizationService>());
        builder.Services.AddSingleton(TimeProvider.System);
        await using var app = builder.Build();
        app.MapScheduleEndpoints();

        var route = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == pattern
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));

        Assert.Equal(policy, Assert.Single(route.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
    }
}

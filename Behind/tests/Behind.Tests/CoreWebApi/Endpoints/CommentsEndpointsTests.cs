using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class CommentsEndpointsTests
{
    private static WebApplication App(bool? news)
    {
        var builder = WebApplication.CreateBuilder();

        if (news is { } enabled)
        {
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [$"{CommentsConfiguration.SectionName}:News"] = enabled.ToString() });
        }

        builder.Services.Configure<CommentsConfiguration>(
            builder.Configuration.GetSection(CommentsConfiguration.SectionName));

        return builder.Build();
    }

    private static async Task<int> Call(WebApplication app, string entity)
    {
        app.MapGet("/guarded", () => "ok").RequireCommentsOf(entity);

        var route = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single();

        using var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(route);

        await route.RequestDelegate!(context);

        return context.Response.StatusCode;
    }

    [Fact]
    public async Task RequireCommentsOf_EnabledByDefault_RunsTheEndpoint()
    {
        Assert.Equal(200, await Call(App(null), CommentsConfiguration.NewsEntity));
    }

    [Fact]
    public async Task RequireCommentsOf_SwitchedOff_AnswersNotFound()
    {
        Assert.Equal(404, await Call(App(false), CommentsConfiguration.NewsEntity));
    }

    [Fact]
    public async Task RequireCommentsOf_SwitchedOnExplicitly_RunsTheEndpoint()
    {
        Assert.Equal(200, await Call(App(true), CommentsConfiguration.NewsEntity));
    }

    [Fact]
    public async Task RequireCommentsOf_UnknownEntity_AnswersNotFound()
    {
        Assert.Equal(404, await Call(App(null), "Unknown"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetSettings_ReportsTheSwitchOfTheNews(bool enabled)
    {
        var result = CommentsEndpoints.GetSettings(
            FixedOptionsSnapshot.Of(new CommentsConfiguration { News = enabled }));

        Assert.Equal(
            new CommentsSettingsResponse(enabled),
            Assert.IsType<JsonHttpResult<CommentsSettingsResponse>>(result).Value);
    }
}

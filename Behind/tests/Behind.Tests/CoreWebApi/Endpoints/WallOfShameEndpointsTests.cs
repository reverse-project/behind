using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class WallOfShameEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_ListsActiveBansWithoutRequiringASession()
    {
        var queries = Substitute.For<IWallOfShameQueries>();
        queries.GetActive(new Flipper(1, WallOfShameEndpoints.DefaultPageSize), Now, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IWallOfShameQueries.ShamedUser>(
                2,
                1,
                1,
                WallOfShameEndpoints.DefaultPageSize,
                Some<IEnumerable<IWallOfShameQueries.ShamedUser>>(
                [
                    new IWallOfShameQueries.ShamedUser("Ada", "spam", None),
                    new IWallOfShameQueries.ShamedUser("Bob", "abuse", Some(Now.AddDays(1)))
                ])));
        var context = new DefaultHttpContext();

        var result = await WallOfShameEndpoints.Get(context, queries, new FixedTimeProvider(Now));

        var page = Assert.IsType<JsonHttpResult<PageResponse<WallOfShameItem>>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(
            [new WallOfShameItem("Ada", "spam", null), new WallOfShameItem("Bob", "abuse", Now.AddDays(1))],
            page.Items);
    }

    [Fact]
    public async Task Get_ClampsThePageSize()
    {
        var queries = Substitute.For<IWallOfShameQueries>();
        queries.GetActive(new Flipper(2, WallOfShameEndpoints.MaxPageSize), Now, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<IWallOfShameQueries.ShamedUser>(0, 2, 0, WallOfShameEndpoints.MaxPageSize, None));

        var result = await WallOfShameEndpoints.Get(
            new DefaultHttpContext(),
            queries,
            new FixedTimeProvider(Now),
            page: 2,
            pageSize: 100000);

        var body = Assert.IsType<JsonHttpResult<PageResponse<WallOfShameItem>>>(result).Value!;
        Assert.Equal(2, body.Page);
        Assert.Empty(body.Items);
    }
}

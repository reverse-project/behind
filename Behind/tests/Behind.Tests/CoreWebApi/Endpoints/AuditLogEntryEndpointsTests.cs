using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class AuditLogEntryEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static (IAuditLogEntryQueries Queries, List<IAuditLogEntryQueries.RequestAuditLog> Requests) Queries(
        Option<PagedResult<AuditLogEntry>> result)
    {
        var requests = new List<IAuditLogEntryQueries.RequestAuditLog>();
        var queries = Substitute.For<IAuditLogEntryQueries>();
        queries.GetAuditLog(Arg.Do<IAuditLogEntryQueries.RequestAuditLog>(requests.Add), Arg.Any<CancellationToken>())
            .Returns(result);

        return (queries, requests);
    }

    private static AuditLogEntry Entry(AuditLogEntry.AuditAction action, User user, Option<User> behalfOf = default) =>
        new("Role", "42", action, user, Now, behalfOf);

    private static Option<PagedResult<AuditLogEntry>> Page(params AuditLogEntry[] entries) =>
        Some(new PagedResult<AuditLogEntry>(entries.Length, 1, 1, 20, Some<IEnumerable<AuditLogEntry>>(entries)));

    [Fact]
    public async Task Get_WithoutParameters_ListsTheLastWeekNewestFirst()
    {
        var (queries, requests) = Queries(None);
        var context = new DefaultHttpContext();

        await AuditLogEntryEndpoints.Get(context, queries, new FixedTimeProvider(Now));

        var request = Assert.Single(requests);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(new DateOnly(2026, 11, 1), request.ToDate);
        Assert.Equal(new DateOnly(2026, 10, 25), request.FromDate);
        Assert.Equal(new Flipper(1, AuditLogEntryEndpoints.DefaultPageSize), request.Flipper);
        Assert.IsType<EmptyFilter<AuditLogEntry>>(request.Filter);
        Assert.Equal(
            [new OrderBy("audit.Timestamp", Direction.Descending)],
            request.OrderByCollection.Collection);
    }

    [Fact]
    public async Task Get_OldestFirst_OrdersAscending()
    {
        var (queries, requests) = Queries(None);

        await AuditLogEntryEndpoints.Get(new DefaultHttpContext(), queries, new FixedTimeProvider(Now), oldestFirst: true);

        Assert.Equal(
            [new OrderBy("audit.Timestamp", Direction.Ascending)],
            Assert.Single(requests).OrderByCollection.Collection);
    }

    [Fact]
    public async Task Get_WithSearch_FiltersTheEntries()
    {
        var (queries, requests) = Queries(None);

        await AuditLogEntryEndpoints.Get(new DefaultHttpContext(), queries, new FixedTimeProvider(Now), search: "  role ");

        Assert.IsNotType<EmptyFilter<AuditLogEntry>>(Assert.Single(requests).Filter);
    }

    [Fact]
    public async Task Get_WithExplicitRangeAndPaging_PassesThemOn()
    {
        var (queries, requests) = Queries(None);

        await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(),
            queries,
            new FixedTimeProvider(Now),
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 3),
            page: 3,
            pageSize: 100000);

        var request = Assert.Single(requests);
        Assert.Equal(new DateOnly(2026, 1, 2), request.FromDate);
        Assert.Equal(new DateOnly(2026, 1, 3), request.ToDate);
        Assert.Equal(new Flipper(3, AuditLogEntryEndpoints.MaxPageSize), request.Flipper);
    }

    [Fact]
    public async Task Get_FromAfterTo_IsABadRequest()
    {
        var (queries, requests) = Queries(None);

        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(),
            queries,
            new FixedTimeProvider(Now),
            new DateOnly(2026, 1, 3),
            new DateOnly(2026, 1, 2));

        Assert.IsType<BadRequest>(result);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task Get_NoEntries_ReturnsAnEmptyPage()
    {
        var (queries, _) = Queries(None);

        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(),
            queries,
            new FixedTimeProvider(Now),
            page: 2,
            pageSize: 5);

        var body = Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!;
        Assert.Equal(0, body.Total);
        Assert.Equal(2, body.Page);
        Assert.Equal(5, body.PageSize);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task Get_Entries_AreMappedToItems()
    {
        var ada = new HumanUser("Ada", [new Email("ada@example.com")], None);
        var (queries, _) = Queries(Page(
            Entry(new AuditLogEntry.Created(), ada),
            Entry(
                new AuditLogEntry.Updated([new PropertyDifference("Name", "old", null)]),
                new SystemUser(None),
                Some<User>(ada))));

        var result = await AuditLogEntryEndpoints.Get(new DefaultHttpContext(), queries, new FixedTimeProvider(Now));

        var body = Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!;
        Assert.Equal(2, body.Total);
        Assert.Equal(
            new AuditLogItem(
                Now,
                "Role",
                "42",
                "Created",
                [],
                new AuditLogActor("HumanUser", "Ada", ["ada@example.com"]),
                null),
            body.Items[0],
            AuditLogItemComparer.Instance);
        Assert.Equal("Updated", body.Items[1].Action);
        Assert.Equal([new AuditLogChange("Name", "old", null)], body.Items[1].Changes);
        Assert.Equal("System", body.Items[1].Actor.Label);
        Assert.Equal("Ada", body.Items[1].OnBehalfOf!.Label);
    }

    private sealed class AuditLogItemComparer : IEqualityComparer<AuditLogItem>
    {
        public static readonly AuditLogItemComparer Instance = new();

        public bool Equals(AuditLogItem? x, AuditLogItem? y) =>
            x is not null
            && y is not null
            && x.Timestamp == y.Timestamp
            && x.EntityName == y.EntityName
            && x.EntityKey == y.EntityKey
            && x.Action == y.Action
            && x.Changes.SequenceEqual(y.Changes)
            && x.Actor.Kind == y.Actor.Kind
            && x.Actor.Label == y.Actor.Label
            && x.Actor.Emails.SequenceEqual(y.Actor.Emails)
            && x.OnBehalfOf is null == y.OnBehalfOf is null;

        public int GetHashCode(AuditLogItem obj) => obj.Timestamp.GetHashCode();
    }
}

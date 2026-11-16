using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
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
        queries.ResolveLabels(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(AuditLabels.Empty);

        return (queries, requests);
    }

    private static AuditLogEntry Entry(AuditLogEntry.AuditAction action, User user, Option<User> behalfOf = default) =>
        new("Role", "42", action, user, Now, behalfOf);

    private static Option<PagedResult<AuditLogEntry>> Page(params AuditLogEntry[] entries) =>
        Some(new PagedResult<AuditLogEntry>(entries.Length, 1, 1, 20, Some<IEnumerable<AuditLogEntry>>(entries)));

    [Fact]
    public async Task GetEntityTypes_ReturnsRecordedTypesWithoutCaching()
    {
        var (queries, _) = Queries(None);
        queries.GetEntityTypes(Arg.Any<CancellationToken>()).Returns(new List<string> { "Role", "User" });
        var context = new DefaultHttpContext();

        var result = await AuditLogEntryEndpoints.GetEntityTypes(context, queries);

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(["Role", "User"], Assert.IsType<JsonHttpResult<IReadOnlyList<string>>>(result).Value);
    }

    [Fact]
    public async Task Get_ExactEntityFilter_DoesNotMatchOtherTypes()
    {
        var (queries, _) = Queries(Page(
            new AuditLogEntry("User", "1", new AuditLogEntry.Created(), Now),
            new AuditLogEntry("UserRole", "2", new AuditLogEntry.Created(), Now)));
        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now), entity: "User", entityExact: true);

        var page = Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!;
        Assert.Equal("User", Assert.Single(page.Items).EntityName);
    }

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
        var (queries, requests) = Queries(Page(
            Entry(new AuditLogEntry.Created(), new GuestUser(None)),
            new AuditLogEntry("User", "123", new AuditLogEntry.Created(), Now)));

        var result = await AuditLogEntryEndpoints.Get(new DefaultHttpContext(), queries, new FixedTimeProvider(Now), search: "  role ");

        Assert.IsType<EmptyFilter<AuditLogEntry>>(Assert.Single(requests).Filter);
        Assert.Equal("Role", Assert.Single(Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!.Items).EntityName);
    }

    [Theory]
    [InlineData("sortBy", "unknown")]
    [InlineData("action", "unknown")]
    public async Task Get_InvalidOptions_AreRejected(string parameter, string value)
    {
        var (queries, requests) = Queries(None);
        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now),
            sortBy: parameter == "sortBy" ? value : null,
            action: parameter == "action" ? value : null);

        Assert.IsType<BadRequest>(result);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task Get_AdvancedSearchFiltersAndSortsBeforePaging()
    {
        using var factory = new TestDbContextFactory();
        Guid roleId;
        await using (var seed = factory.CreateContext())
        {
            roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "Editors");
            var actorId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "actor@example.test");
            var behalfId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "behalf@example.test");
            for (var index = 0; index < 105; index++)
            {
                var entry = new AuditLogEntry(
                    "Role", $"{roleId}:{index:D3}",
                    new AuditLogEntry.Updated([new PropertyDifference("Title", "before", "after")]), Now);
                await seed.AuditLogEntries.AddAsync(entry);
                seed.Entry(entry).Property("UserId").CurrentValue = actorId;
                seed.Entry(entry).Property("BehalfOfId").CurrentValue = behalfId;
            }
            await seed.SaveChangesAsync();
        }

        await using var context = factory.CreateContext();
        var queries = new AuditLogEntryQueries(context, []);
        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now),
            search: "editors", entity: "rol", action: "Updated", actor: "actor@example",
            behalfOf: "behalf@example", sortBy: "key", descending: true, page: 2, pageSize: 10);
        var page = Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!;

        Assert.Equal(105, page.Total);
        Assert.Equal(11, page.PageCount);
        Assert.Equal(10, page.Items.Count);
        Assert.EndsWith(":094", page.Items[0].EntityKey);
        Assert.EndsWith(":085", page.Items[9].EntityKey);
        Assert.All(page.Items, item => Assert.StartsWith("Editors: ", item.EntityLabel));
    }

    [Theory]
    [InlineData("actor@example")]
    [InlineData("behalf@example")]
    [InlineData("title")]
    [InlineData("before")]
    [InlineData("after")]
    public async Task Get_SearchMatchesPeopleAndChanges(string term)
    {
        var actor = new HumanUser("Ada", [new Email("actor@example.test")], None);
        var behalf = new HumanUser("Bob", [new Email("behalf@example.test")], None);
        var (queries, _) = Queries(Page(
            Entry(new AuditLogEntry.Updated([new PropertyDifference("Title", "before", "after")]), actor, Some<User>(behalf))));

        var result = await AuditLogEntryEndpoints.Get(new DefaultHttpContext(), queries, new FixedTimeProvider(Now), search: term);

        Assert.Single(Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!.Items);
    }

    [Fact]
    public async Task Get_SearchMatchesResolvedChangedValues()
    {
        using var factory = new TestDbContextFactory();
        await using (var seed = factory.CreateContext())
        {
            var roleId = await DatabaseSeedHelper.SeedUserRoleAsync(seed, "Editors");
            await seed.AuditLogEntries.AddAsync(new AuditLogEntry(
                "Other", "plain",
                new AuditLogEntry.Updated([new PropertyDifference("Role", "old", roleId.ToString())]), Now));
            await seed.SaveChangesAsync();
        }

        await using var context = factory.CreateContext();
        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), new AuditLogEntryQueries(context, []), new FixedTimeProvider(Now), search: "editors");

        var item = Assert.Single(Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!.Items);
        Assert.Equal("Editors", Assert.Single(item.Changes).NewValue);
    }

    [Theory]
    [InlineData("Deleted", "", "", "")]
    [InlineData("", "another actor", "", "")]
    [InlineData("", "", "another person", "")]
    [InlineData("", "", "", "another entity")]
    public async Task Get_FiltersExcludeNonmatchingEntries(string action, string actor, string behalfOf, string entity)
    {
        var (queries, _) = Queries(Page(Entry(new AuditLogEntry.Created(), new GuestUser(None))));
        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now),
            action: action, actor: actor, behalfOf: behalfOf, entity: entity);

        var page = Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!;
        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Get_EarlyDatesAndMaximumDate_AreHandled()
    {
        var (queries, requests) = Queries(None);
        await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now), toDate: DateOnly.MinValue);
        Assert.Equal(DateOnly.MinValue, Assert.Single(requests).FromDate);

        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now), toDate: DateOnly.MaxValue);
        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Get_PagingOverflow_IsRejected()
    {
        var (queries, requests) = Queries(None);
        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now), page: int.MaxValue, pageSize: 100);

        Assert.IsType<BadRequest>(result);
        Assert.Empty(requests);
    }

    [Theory]
    [InlineData("entity", false, "Alpha")]
    [InlineData("entity", true, "Zulu")]
    [InlineData("key", false, "Zulu")]
    [InlineData("action", false, "Alpha")]
    [InlineData("actor", false, "Alpha")]
    public async Task Get_SortsBySelectedField(string sortBy, bool descending, string expectedEntity)
    {
        var (queries, _) = Queries(Page(
            new AuditLogEntry("Zulu", "a", new AuditLogEntry.Updated([]), new HumanUser("Zoe", [new Email("zoe@example.test")], None), Now, None),
            new AuditLogEntry("Alpha", "z", new AuditLogEntry.Created(), new HumanUser("Ada", [new Email("ada@example.test")], None), Now, None)));

        var result = await AuditLogEntryEndpoints.Get(
            new DefaultHttpContext(), queries, new FixedTimeProvider(Now), sortBy: sortBy, descending: descending);

        Assert.Equal(expectedEntity, Assert.IsType<JsonHttpResult<PageResponse<AuditLogItem>>>(result).Value!.Items[0].EntityName);
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
            && x.EntityLabel == y.EntityLabel
            && x.Action == y.Action
            && x.Changes.SequenceEqual(y.Changes)
            && x.Actor.Kind == y.Actor.Kind
            && x.Actor.Label == y.Actor.Label
            && x.Actor.Emails.SequenceEqual(y.Actor.Emails)
            && x.OnBehalfOf is null == y.OnBehalfOf is null;

        public int GetHashCode(AuditLogItem obj) => obj.Timestamp.GetHashCode();
    }
}

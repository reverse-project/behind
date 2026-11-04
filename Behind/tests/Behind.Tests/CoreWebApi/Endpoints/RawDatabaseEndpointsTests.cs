using System.Text.Json;
using Behind.Common.Database.Raw;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class RawDatabaseEndpointsTests
{
    private static readonly RawTable Grants = new(
        "Grants",
        null,
        "Grant",
        [
            new RawColumn("Id", "TEXT", RawValueKind.Guid, false, true, false),
            new RawColumn("Name", "TEXT", RawValueKind.Text, false, false, false)
        ]);

    private static Dictionary<string, JsonElement> Values(string name, object value) =>
        new() { [name] = JsonSerializer.SerializeToElement(value) };

    private static RawRow Row(string name) =>
        new(new Dictionary<string, object?> { ["Id"] = "id-1", ["Name"] = name });

    [Fact]
    public async Task Tables_ListsTheTablesAndIsNotCached()
    {
        var queries = Substitute.For<IRawDatabaseQueries>();
        queries.ListTables(default).ReturnsForAnyArgs([new IRawDatabaseQueries.RawTableListing(Grants, 3, false)]);
        var context = new DefaultHttpContext();

        var result = Assert.IsType<JsonHttpResult<RawTablesResponse>>(await RawDatabaseEndpoints.Tables(context, queries));

        var table = Assert.Single(result.Value!.Tables);
        Assert.Equal("Grants", table.Name);
        Assert.Equal("Grant", table.EntityName);
        Assert.Equal(3, table.Rows);
        Assert.False(table.IsReadOnly);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(["guid", "text"], table.Columns.Select(column => column.Kind));
        Assert.Equal([true, false], table.Columns.Select(column => column.IsKey));
    }

    [Fact]
    public async Task Rows_UnknownTable_IsNotFound()
    {
        var queries = Substitute.For<IRawDatabaseQueries>();
        queries.ListRows(default!, default!, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await RawDatabaseEndpoints.Rows("Nope", new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task Rows_PassesPagingSearchAndOrderToTheQuery()
    {
        var queries = Substitute.For<IRawDatabaseQueries>();
        queries.ListRows(default!, default!, default).ReturnsForAnyArgs(
            new PagedResult<RawRow>(1, 2, 1, 5, Some<IEnumerable<RawRow>>([Row("read")])));

        var result = await RawDatabaseEndpoints.Rows(
            "Grants",
            new DefaultHttpContext(),
            queries,
            2,
            5,
            "  rea  ",
            "Name",
            true);

        var json = Assert.IsType<JsonHttpResult<Behind.Common.WebApi.PageResponse<IReadOnlyDictionary<string, object?>>>>(result);
        Assert.Equal("read", Assert.Single(json.Value!.Items)["Name"]);
        await queries.Received(1).ListRows(
            "Grants",
            Arg.Is<RawRowQuery>(query =>
                query.Flipper.Index == 2
                && query.Flipper.Size == 5
                && query.Search == Some("rea")
                && query.OrderBy == Some("Name")
                && query.Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rows_PageSizeIsCapped()
    {
        var queries = Substitute.For<IRawDatabaseQueries>();
        queries.ListRows(default!, default!, default).ReturnsForAnyArgs(
            new PagedResult<RawRow>(0, 1, 0, 100, None));

        await RawDatabaseEndpoints.Rows("Grants", new DefaultHttpContext(), queries, pageSize: 100_000);

        await queries.Received(1).ListRows(
            "Grants",
            Arg.Is<RawRowQuery>(query => query.Flipper.Size == RawDatabaseEndpoints.MaxPageSize),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Insert_Saved_AnswersTheRow()
    {
        var commands = Substitute.For<IRawDatabaseCommands>();
        commands.Insert(default!, default!, default).ReturnsForAnyArgs(new RawWriteResult.Saved(Row("read")));

        var result = await RawDatabaseEndpoints.Insert(
            "Grants",
            new RawDatabaseEndpoints.InsertRowRequest(Values("Name", "read")),
            new DefaultHttpContext(),
            commands);

        var json = Assert.IsType<JsonHttpResult<IReadOnlyDictionary<string, object?>>>(result);
        Assert.Equal("read", json.Value!["Name"]);
        await commands.Received(1).Insert("Grants", Arg.Any<IReadOnlyDictionary<string, JsonElement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_PassesTheKeyAndTheValues()
    {
        var commands = Substitute.For<IRawDatabaseCommands>();
        commands.Update(default!, default!, default!, default).ReturnsForAnyArgs(new RawWriteResult.Saved(Row("write")));

        await RawDatabaseEndpoints.Update(
            "Grants",
            new RawDatabaseEndpoints.UpdateRowRequest(Values("Id", "id-1"), Values("Name", "write")),
            new DefaultHttpContext(),
            commands);

        await commands.Received(1).Update(
            "Grants",
            Arg.Is<IReadOnlyDictionary<string, JsonElement>>(key => key.ContainsKey("Id")),
            Arg.Is<IReadOnlyDictionary<string, JsonElement>>(values => values.ContainsKey("Name")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Deleted_IsNoContent()
    {
        var commands = Substitute.For<IRawDatabaseCommands>();
        commands.Delete(default!, default!, default).ReturnsForAnyArgs(new RawWriteResult.Deleted());

        var result = await RawDatabaseEndpoints.Delete(
            "Grants",
            new RawDatabaseEndpoints.DeleteRowRequest(Values("Id", "id-1")),
            new DefaultHttpContext(),
            commands);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public void ToResult_MapsEveryWriteResultToItsStatus()
    {
        Assert.IsType<NotFound>(new RawWriteResult.NotFound().ToResult());

        var readOnly = Assert.IsType<JsonHttpResult<RawConflictResponse>>(new RawWriteResult.ReadOnly().ToResult());
        Assert.Equal(409, readOnly.StatusCode);
        Assert.Equal(RawDatabaseResponseExtensions.ReadOnlyReason, readOnly.Value!.Reason);

        var invalid = Assert.IsType<JsonHttpResult<RawInvalidResponse>>(new RawWriteResult.Invalid(["bad"]).ToResult());
        Assert.Equal(400, invalid.StatusCode);
        Assert.Equal(["bad"], invalid.Value!.Errors);

        var rejected = Assert.IsType<JsonHttpResult<RawConflictResponse>>(new RawWriteResult.Rejected("unique").ToResult());
        Assert.Equal(409, rejected.StatusCode);
        Assert.Equal("unique", rejected.Value!.Message);
        Assert.Equal(RawDatabaseResponseExtensions.RejectedReason, rejected.Value.Reason);
    }
}

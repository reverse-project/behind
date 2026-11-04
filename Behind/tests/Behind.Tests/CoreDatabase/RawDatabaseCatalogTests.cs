using System.Text.Json;
using Behind.Common.Database.Raw;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Behind.Tests.CoreDatabase;

public sealed class RawDatabaseCatalogTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void RawTables_CoverEveryMappedTableOfTheCoreModel()
    {
        using var context = _factory.CreateContext();
        var mapped = context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables
            .Where(table => table.PrimaryKey is not null)
            .Select(table => table.Name)
            .ToHashSet();
        var exposed = context.RawTables.Select(table => table.Name).ToHashSet();

        Assert.Equal(mapped, exposed);
        Assert.Contains("Grants", exposed);
        Assert.Contains("Users", exposed);
        Assert.Contains("Sites", exposed);
        Assert.Contains("UserRoles", exposed);
    }

    [Fact]
    public void RawTables_EveryEntityTypeOfTheModelBelongsToAnExposedTable()
    {
        using var context = _factory.CreateContext();
        var exposed = context.RawTables.Select(table => table.Name).ToHashSet();
        var unexposed = context.Model.GetEntityTypes()
            .Where(entityType => entityType.GetTableName() is not null)
            .Where(entityType => !exposed.Contains(entityType.GetTableName()!))
            .Select(entityType => entityType.DisplayName())
            .ToList();

        Assert.Empty(unexposed);
    }

    [Fact]
    public void RawTables_EveryTableHasAKeyAndColumns()
    {
        using var context = _factory.CreateContext();

        Assert.All(context.RawTables, table =>
        {
            Assert.NotEmpty(table.KeyColumns);
            Assert.NotEmpty(table.Columns);
            Assert.False(string.IsNullOrWhiteSpace(table.EntityName));
        });
    }

    [Fact]
    public void RawTables_DescribeTheColumnsOfATable()
    {
        using var context = _factory.CreateContext();
        var grants = context.FindRawTable("Grants").Match(() => throw new InvalidOperationException(), table => table);

        Assert.Equal("Grant", grants.EntityName);
        Assert.Equal(["Id"], grants.KeyColumns.Select(column => column.Name));
        Assert.Equal(RawValueKind.Guid, grants.FindColumn("Id").Match(() => RawValueKind.Text, column => column.Kind));
        Assert.Equal(RawValueKind.Text, grants.FindColumn("Name").Match(() => RawValueKind.Integer, column => column.Kind));
    }

    [Fact]
    public void RawTables_FlagSensibleColumns()
    {
        using var context = _factory.CreateContext();
        var passwords = context.FindRawTable("Passwords").Match(() => throw new InvalidOperationException(), table => table);

        Assert.True(passwords.FindColumn("Value").Match(() => false, column => column.IsSensitive));
        Assert.False(passwords.FindColumn("UserId").Match(() => true, column => column.IsSensitive));
    }

    [Fact]
    public void RawTables_NameSharedTypeTablesAfterTheirEntity()
    {
        using var context = _factory.CreateContext();
        var userRoles = context.FindRawTable("UserRoles").Match(() => throw new InvalidOperationException(), table => table);

        Assert.Equal("UserRoles", userRoles.EntityName);
    }

    [Fact]
    public async Task EveryTable_CanBeListedSearchedAndSorted()
    {
        await using var context = _factory.CreateContext();
        var queries = new RawDatabaseQueries(context);

        foreach (var table in context.RawTables)
        {
            var query = new RawRowQuery(new Flipper(1, 5), "a", table.Columns[0].Name, true);
            var page = await queries.ListRows(table.Name, query);

            Assert.True(page.Match(() => false, _ => true), table.Name);
        }
    }

    [Fact]
    public void FindRawTable_UnknownName_IsNone()
    {
        using var context = _factory.CreateContext();

        Assert.True(context.FindRawTable("Nope").Match(() => true, _ => false));
        Assert.True(context.FindRawTable("grants").Match(() => true, _ => false));
    }

    [Fact]
    public void ParseKey_RequiresExactlyTheKeyColumns()
    {
        using var context = _factory.CreateContext();
        var grants = context.FindRawTable("Grants").Match(() => throw new InvalidOperationException(), table => table);
        var id = JsonSerializer.SerializeToElement(Guid.NewGuid());
        var name = JsonSerializer.SerializeToElement("x");

        Assert.True(grants.ParseKey(new Dictionary<string, JsonElement> { ["Id"] = id }).Match(_ => false, _ => true));
        Assert.True(grants.ParseKey(new Dictionary<string, JsonElement>()).Match(_ => true, _ => false));
        Assert.True(grants.ParseKey(new Dictionary<string, JsonElement> { ["Id"] = id, ["Name"] = name }).Match(_ => true, _ => false));
        Assert.True(grants.ParseKey(new Dictionary<string, JsonElement> { ["Id"] = JsonSerializer.SerializeToElement((string?)null) }).Match(_ => true, _ => false));
    }
}

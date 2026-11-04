using System.Text.Json;
using Behind.Common.Database.Raw;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class RawDatabaseCommandsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);

    private static Dictionary<string, JsonElement> Values(params (string Name, object? Value)[] values) =>
        values.ToDictionary(pair => pair.Name, pair => Json(pair.Value));

    private CoreContextSqlite Audited() => _factory.CreateAuditedContext(AuditActor.Anonymous);

    private static RawDatabaseCommands Commands(CoreContextSqlite context, AuditActor? actor = null) =>
        new(context, new FixedAuditActorProvider(actor ?? AuditActor.Anonymous), TimeProvider.System);

    private async Task<RawWriteResult> InsertGrantAsync(Guid id, string name)
    {
        await using var context = Audited();

        return await Commands(context).Insert("Grants", Values(("Id", id), ("Name", name)));
    }

    private async Task<List<AuditLogEntry>> AuditOfAsync(string entityName, string key)
    {
        await using var context = _factory.CreateContext();

        return await context.AuditLogEntries
            .Where(audit => audit.EntityName == entityName && audit.EntityKey == key)
            .ToListAsync();
    }

    private async Task<List<string>> GrantNamesAsync()
    {
        await using var context = _factory.CreateContext();

        return await context.Grants.Select(grant => grant.Name).ToListAsync();
    }

    [Fact]
    public async Task Insert_AddsTheRowAndAuditsItAsCreated()
    {
        var id = Guid.NewGuid();

        var result = await InsertGrantAsync(id, "Raw.Insert");

        var saved = Assert.IsType<RawWriteResult.Saved>(result);
        Assert.Equal("Raw.Insert", saved.Row.Values["Name"]);
        Assert.Equal(id.ToString(), saved.Row.Values["Id"]);
        Assert.Contains("Raw.Insert", await GrantNamesAsync());

        var audit = Assert.Single(await AuditOfAsync("Grant", id.ToString()));
        Assert.IsType<AuditLogEntry.Created>(audit.Action);
    }

    [Fact]
    public async Task Insert_AuditsTheActorOfTheRequest()
    {
        await using var seed = _factory.CreateContext();
        var actorId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "raw-actor@test.com");
        var id = Guid.NewGuid();

        await using var context = Audited();
        await Commands(context, AuditActor.ForUser(actorId)).Insert("Grants", Values(("Id", id), ("Name", "Raw.Actor")));

        await using var verify = _factory.CreateContext();
        var audit = await verify.AuditLogEntries
            .Where(entry => entry.EntityName == "Grant" && entry.EntityKey == id.ToString())
            .Select(entry => EF.Property<Guid?>(entry, "UserId"))
            .SingleAsync();
        Assert.Equal(actorId, audit);
    }

    [Fact]
    public async Task Insert_WithoutGuidKey_GeneratesIt()
    {
        await using var context = Audited();

        var result = await Commands(context).Insert("Grants", Values(("Name", "Raw.Generated")));

        var saved = Assert.IsType<RawWriteResult.Saved>(result);
        Assert.True(Guid.TryParse((string)saved.Row.Values["Id"]!, out var id));
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Insert_UnknownColumn_IsInvalid()
    {
        await using var context = Audited();

        var result = await Commands(context).Insert("Grants", Values(("Nope", 1)));

        var invalid = Assert.IsType<RawWriteResult.Invalid>(result);
        Assert.Contains(invalid.Errors, error => error.Contains("Nope"));
    }

    [Fact]
    public async Task Insert_ConstraintViolation_IsRejectedAndLeavesNoAudit()
    {
        var id = Guid.NewGuid();
        await InsertGrantAsync(id, "Raw.Unique");

        var duplicateId = Guid.NewGuid();
        var result = await InsertGrantAsync(duplicateId, "Raw.Unique");

        Assert.IsType<RawWriteResult.Rejected>(result);
        Assert.Empty(await AuditOfAsync("Grant", duplicateId.ToString()));
    }

    [Fact]
    public async Task Update_ChangesTheRowAndAuditsTheDifferences()
    {
        var id = Guid.NewGuid();
        await InsertGrantAsync(id, "Raw.Before");

        await using var context = Audited();
        var result = await Commands(context).Update("Grants", Values(("Id", id)), Values(("Name", "Raw.After")));

        var saved = Assert.IsType<RawWriteResult.Saved>(result);
        Assert.Equal("Raw.After", saved.Row.Values["Name"]);
        Assert.Contains("Raw.After", await GrantNamesAsync());
        Assert.DoesNotContain("Raw.Before", await GrantNamesAsync());

        var updated = Assert.Single(
            (await AuditOfAsync("Grant", id.ToString())).Select(audit => audit.Action).OfType<AuditLogEntry.Updated>());
        var difference = Assert.Single(updated.Differences);
        Assert.Equal("Name", difference.PropertyName);
        Assert.Equal("Raw.Before", difference.OldValue?.ToString());
        Assert.Equal("Raw.After", difference.NewValue?.ToString());
    }

    [Fact]
    public async Task Update_WithSameValues_WritesNoAudit()
    {
        var id = Guid.NewGuid();
        await InsertGrantAsync(id, "Raw.Same");

        await using var context = Audited();
        var result = await Commands(context).Update("Grants", Values(("Id", id)), Values(("Name", "Raw.Same")));

        Assert.IsType<RawWriteResult.Saved>(result);
        Assert.DoesNotContain(
            (await AuditOfAsync("Grant", id.ToString())).Select(audit => audit.Action),
            action => action is AuditLogEntry.Updated);
    }

    [Fact]
    public async Task Update_KeyColumn_IsInvalid()
    {
        var id = Guid.NewGuid();
        await InsertGrantAsync(id, "Raw.Key");

        await using var context = Audited();
        var result = await Commands(context).Update("Grants", Values(("Id", id)), Values(("Id", Guid.NewGuid())));

        Assert.IsType<RawWriteResult.Invalid>(result);
    }

    [Fact]
    public async Task Update_WithoutValues_IsInvalid()
    {
        await using var context = Audited();

        var result = await Commands(context).Update("Grants", Values(("Id", Guid.NewGuid())), Values());

        Assert.IsType<RawWriteResult.Invalid>(result);
    }

    [Fact]
    public async Task Update_MissingRow_IsNotFound()
    {
        await using var context = Audited();

        var result = await Commands(context).Update("Grants", Values(("Id", Guid.NewGuid())), Values(("Name", "Raw.None")));

        Assert.IsType<RawWriteResult.NotFound>(result);
    }

    [Fact]
    public async Task Delete_RemovesTheRowAndAuditsItAsHardDeleted()
    {
        var id = Guid.NewGuid();
        await InsertGrantAsync(id, "Raw.Delete");

        await using var context = Audited();
        var result = await Commands(context).Delete("Grants", Values(("Id", id)));

        Assert.IsType<RawWriteResult.Deleted>(result);
        Assert.DoesNotContain("Raw.Delete", await GrantNamesAsync());
        Assert.Contains(
            (await AuditOfAsync("Grant", id.ToString())).Select(audit => audit.Action),
            action => action is AuditLogEntry.HardDeleted);
    }

    [Fact]
    public async Task Delete_MissingRow_IsNotFound()
    {
        await using var context = Audited();

        var result = await Commands(context).Delete("Grants", Values(("Id", Guid.NewGuid())));

        Assert.IsType<RawWriteResult.NotFound>(result);
    }

    [Fact]
    public async Task Delete_WithIncompleteKey_IsInvalid()
    {
        await using var context = Audited();

        var result = await Commands(context).Delete("Grants", Values());

        Assert.IsType<RawWriteResult.Invalid>(result);
    }

    [Fact]
    public async Task Write_UnknownTable_IsNotFound()
    {
        await using var context = Audited();

        Assert.IsType<RawWriteResult.NotFound>(await Commands(context).Insert("Nope", Values(("Name", "x"))));
        Assert.IsType<RawWriteResult.NotFound>(await Commands(context).Delete("Nope", Values(("Id", 1))));
    }

    [Fact]
    public async Task Write_AuditLogTable_IsReadOnly()
    {
        await using var context = Audited();

        var result = await Commands(context).Delete("AuditLogEntries", Values(("Id", Guid.NewGuid())));

        Assert.IsType<RawWriteResult.ReadOnly>(result);
    }

    [Fact]
    public async Task Update_SensitiveColumn_AuditsMaskedValuesAndIgnoresTheMask()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "raw-password@test.com");
        var passwordId = Guid.NewGuid();

        await using (var insert = Audited())
        {
            var inserted = await Commands(insert).Insert(
                "Passwords",
                Values(("Id", passwordId), ("UserId", userId), ("Value", "hash-1"), ("IsActive", true)));
            var saved = Assert.IsType<RawWriteResult.Saved>(inserted);
            Assert.Equal(SensibleExtensions.MaskedValue, saved.Row.Values["Value"]);
        }

        await using (var maskedUpdate = Audited())
        {
            await Commands(maskedUpdate).Update(
                "Passwords",
                Values(("Id", passwordId)),
                Values(("Value", SensibleExtensions.MaskedValue), ("IsActive", false)));
        }

        await using (var update = Audited())
        {
            await Commands(update).Update("Passwords", Values(("Id", passwordId)), Values(("Value", "hash-2")));
        }

        await using var verify = _factory.CreateContext();
        var stored = await verify.Database
            .SqlQuery<string>($"SELECT Value FROM Passwords WHERE Id = {passwordId.ToString().ToUpperInvariant()}")
            .ToListAsync();
        Assert.Equal("hash-2", Assert.Single(stored));

        var differences = (await AuditOfAsync("Password", passwordId.ToString()))
            .Select(audit => audit.Action)
            .OfType<AuditLogEntry.Updated>()
            .SelectMany(updated => updated.Differences)
            .Where(difference => difference.PropertyName == "Value")
            .ToList();
        var valueDifference = Assert.Single(differences);
        Assert.Equal(SensibleExtensions.MaskedValue, valueDifference.OldValue?.ToString());
        Assert.Equal(SensibleExtensions.MaskedValue, valueDifference.NewValue?.ToString());
    }

    [Fact]
    public async Task Queries_ListRows_PagesSearchesAndOrders()
    {
        foreach (var name in new[] { "Raw.List.A", "Raw.List.B", "Raw.List.C" })
        {
            await InsertGrantAsync(Guid.NewGuid(), name);
        }

        await using var context = _factory.CreateContext();
        var queries = new RawDatabaseQueries(context);

        var page = (await queries.ListRows(
                "Grants",
                new RawRowQuery(new Flipper(1, 2), "Raw.List", "Name", true)))
            .Match(() => throw new InvalidOperationException(), result => result);

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.PageCount);
        var first = page.Results.Match(() => [], rows => rows.ToList());
        Assert.Equal(["Raw.List.C", "Raw.List.B"], first.Select(row => row.Values["Name"]));

        var second = (await queries.ListRows(
                "Grants",
                new RawRowQuery(new Flipper(2, 2), "Raw.List", "Name", true)))
            .Match(() => throw new InvalidOperationException(), result => result);
        Assert.Equal(
            ["Raw.List.A"],
            second.Results.Match(() => [], rows => rows.Select(row => row.Values["Name"]).ToList()));
    }

    [Fact]
    public async Task Queries_ListRows_TreatsLikeWildcardsLiterally()
    {
        await InsertGrantAsync(Guid.NewGuid(), "Raw.Wild_card");
        await InsertGrantAsync(Guid.NewGuid(), "Raw.WildXcard");

        await using var context = _factory.CreateContext();
        var page = (await new RawDatabaseQueries(context).ListRows(
                "Grants",
                new RawRowQuery(new Flipper(1, 10), "Wild_card", None, false)))
            .Match(() => throw new InvalidOperationException(), result => result);

        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task Queries_ListRows_UnknownTable_IsNone()
    {
        await using var context = _factory.CreateContext();

        var page = await new RawDatabaseQueries(context).ListRows(
            "Nope",
            new RawRowQuery(new Flipper(1, 10), None, None, false));

        Assert.True(page.Match(() => true, _ => false));
    }

    [Fact]
    public async Task Queries_ListRows_MasksSensitiveColumns()
    {
        await using var seed = _factory.CreateContext();
        var userId = await DatabaseSeedHelper.SeedHumanUserAsync(seed, "raw-masked@test.com");

        await using (var insert = Audited())
        {
            await Commands(insert).Insert(
                "Passwords",
                Values(("UserId", userId), ("Value", "very-secret"), ("IsActive", true)));
        }

        await using var context = _factory.CreateContext();
        var page = (await new RawDatabaseQueries(context).ListRows(
                "Passwords",
                new RawRowQuery(new Flipper(1, 50), None, None, false)))
            .Match(() => throw new InvalidOperationException(), result => result);

        var rows = page.Results.Match(() => [], found => found.ToList());
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(SensibleExtensions.MaskedValue, row.Values["Value"]));
    }

    [Fact]
    public async Task Queries_ListTables_ReportsRowCountsAndReadOnlyFlags()
    {
        await InsertGrantAsync(Guid.NewGuid(), "Raw.Count");

        await using var context = _factory.CreateContext();
        var tables = await new RawDatabaseQueries(context).ListTables();

        Assert.Equal(context.RawTables.Count, tables.Count);
        Assert.True(tables.Single(listing => listing.Table.Name == "Grants").Rows >= 1);
        Assert.True(tables.Single(listing => listing.Table.Name == "AuditLogEntries").IsReadOnly);
        Assert.False(tables.Single(listing => listing.Table.Name == "Grants").IsReadOnly);
    }
}

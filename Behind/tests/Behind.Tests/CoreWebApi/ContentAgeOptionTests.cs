using Behind.Common.Database.Raw;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentAgeOptionTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private static readonly AuditActor Actor = AuditActor.Anonymous;

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(151)]
    public async Task CreateRejectsInvalidAges(int age)
    {
        await using var context = _factory.CreateAuditedContext(Actor);
        Assert.IsType<BadRequest>(await ContentAgeOptionEndpoints.Create(new(age, age.ToString()), new DefaultHttpContext(), context));
        Assert.Empty(await context.ContentAgeOptions.ToListAsync());
        Assert.Empty(await context.AuditLogEntries.ToListAsync());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(150)]
    public async Task CreatePersistsVersionedAuditedOptionsAndRejectsActiveDuplicates(int age)
    {
        var id = await Create(age);
        await using var context = _factory.CreateAuditedContext(Actor);
        var option = Assert.Single(await context.ContentAgeOptions.ToListAsync());
        Assert.Equal(new VersionedKey<Guid>(id, 1), context.Entry(option).VersionedId);
        Assert.Equal(age, option.Age);
        Assert.Equal(age.ToString(), option.Name);
        Assert.IsType<Conflict>(await ContentAgeOptionEndpoints.Create(new(age, age.ToString()), new DefaultHttpContext(), context));
        Assert.True(await context.IsActiveContentAgeOption(age));
        Assert.False(await context.IsActiveContentAgeOption(0));
        Assert.False(await context.IsActiveContentAgeOption(151));
        var audit = Assert.Single(await context.AuditLogEntries.ToListAsync());
        Assert.Equal(nameof(ContentAgeOption), audit.EntityName);
        Assert.Equal($"{id}:1", audit.EntityKey);
        Assert.IsType<AuditLogEntry.Created>(audit.Action);
        var labels = await context.ResolveAuditLabelsAsync([id], []);
        Assert.Equal($"{age} (minimum age {age})", labels.Label(id.ToString()));
        var table = Assert.Single(context.RawTables, item => item.Name == "ContentAgeOptions");
        Assert.Contains(table.Columns, item => item.Name == "Age");
        Assert.Contains(table.Columns, item => item.Name == "Name");
        Assert.False(context.IsReadOnly(table));
    }

    [Fact]
    public async Task NamesAreTrimmedAndUpdatesPreserveAgeHistoryAndRestrictions()
    {
        var id = await Create(18);
        var restriction = Guid.NewGuid();
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            AddRestriction(context, restriction, 1, 18);
            await context.SaveChangesAsync();
            var result = Assert.IsType<Ok<ContentAgeOptionEndpoints.AgeOptionResponse>>(
                await ContentAgeOptionEndpoints.Update(id, new("  Mature audience  "), new DefaultHttpContext(), context));
            Assert.Equal(new(id, 18, "Mature audience"), result.Value);
        }

        await using var read = _factory.CreateAuditedContext(Actor);
        var versions = await read.ContentAgeOptions.OfKey(id).OrderBy(item => EF.Property<long>(item, "Version")).ToListAsync();
        Assert.Equal(new[] { "18", "Mature audience" }, versions.Select(item => item.Name));
        Assert.All(versions, item => Assert.Equal(18, item.Age));
        Assert.Equal(18, Assert.Single(await read.ContentRestrictions.ToListAsync()).MinimumAge);
        Assert.Equal("Mature audience (minimum age 18)", (await read.ResolveAuditLabelsAsync([id], [])).Label(id.ToString()));
        Assert.Contains(await read.AuditLogEntries.ToListAsync(),
            item => item.EntityName == nameof(ContentAgeOption) && item.EntityKey == $"{id}:2"
                && item.Action is AuditLogEntry.Created);
    }

    [Fact]
    public async Task InvalidNamesAreRejectedOnCreateAndUpdateWithoutWriting()
    {
        var id = await Create(18);
        await using var context = _factory.CreateAuditedContext(Actor);
        foreach (var name in new[] { null, "", "   ", new string('x', 201), "bad\0name", "bad\u0001name", "bad\ud800name" })
        {
            Assert.IsType<BadRequest>(await ContentAgeOptionEndpoints.Create(new(17, name), new DefaultHttpContext(), context));
            Assert.IsType<BadRequest>(await ContentAgeOptionEndpoints.Update(id, new(name), new DefaultHttpContext(), context));
        }
        Assert.Single(await context.ContentAgeOptions.ToListAsync());
        Assert.Single(await context.AuditLogEntries.ToListAsync());
        var result = Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
            await ContentAgeOptionEndpoints.Create(new(17, "  " + new string('x', 200) + "  "), new DefaultHttpContext(), context));
        Assert.Equal(new string('x', 200), result.Value!.Name);
    }

    [Fact]
    public async Task UpdateCannotRestoreDeletedOrMissingOptions()
    {
        var id = await Create(18);
        await using var context = _factory.CreateAuditedContext(Actor);
        Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.Update(Guid.NewGuid(), new("name"), new DefaultHttpContext(), context));
        Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), context));
        Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.Update(id, new("name"), new DefaultHttpContext(), context));
        Assert.False(await context.IsActiveContentAgeOption(18));
    }

    [Fact]
    public async Task ListOrdersActiveOptionsAndCanSelectDeletedOptions()
    {
        var deleted = await Create(18);
        var active = await Create(12);
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(deleted, new DefaultHttpContext(), context));
        }

        await using var read = _factory.CreateContext();
        var http = new DefaultHttpContext();
        var result = Assert.IsType<JsonHttpResult<ContentAgeOptionEndpoints.AgeOptionListResponse>>(
            await ContentAgeOptionEndpoints.List(http, read));
        Assert.Equal(new ContentAgeOptionEndpoints.AgeOptionResponse(active, 12, "12"), Assert.Single(result.Value!.Items));
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        var removed = Assert.IsType<JsonHttpResult<ContentAgeOptionEndpoints.AgeOptionListResponse>>(
            await ContentAgeOptionEndpoints.List(http, read, true));
        Assert.Equal(new ContentAgeOptionEndpoints.AgeOptionResponse(deleted, 18, "18"), Assert.Single(removed.Value!.Items));
        using (read.SoftDelete.IncludeDeleted())
        {
            Assert.False(await read.IsActiveContentAgeOption(18));
        }
    }

    [Fact]
    public async Task DeleteRestoreAndHardDeletePreserveAllRestrictionVersions()
    {
        var id = await Create(18);
        var target = Guid.NewGuid();
        var changed = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var secondTarget = Guid.NewGuid();
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            AddRestriction(context, target, 1, 16);
            AddRestriction(context, target, 2, 18);
            AddRestriction(context, changed, 1, 18);
            AddRestriction(context, changed, 2, 12);
            AddRestriction(context, deleted, 1, 18);
            AddRestriction(context, secondTarget, 1, 18);
            await context.SaveChangesAsync();
            await context.MarkSoftDeletedAsync(context.ContentRestrictions.OfKey(deleted));
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), context));
        }

        await using (var read = _factory.CreateContext())
        {
            var rows = await read.ContentRestrictions.LatestOf(read.ContentRestrictions).ToListAsync();
            Assert.Equal(3, rows.Count);
            Assert.Equal(18, rows.Single(item => read.Entry(item).VersionedId.Key == target).MinimumAge);
            Assert.Equal(2, read.Entry(rows.Single(item => read.Entry(item).VersionedId.Key == target)).VersionedId.Version);
            Assert.Equal(18, rows.Single(item => read.Entry(item).VersionedId.Key == secondTarget).MinimumAge);
            Assert.Equal(12, rows.Single(item => read.Entry(item).VersionedId.Key == changed).MinimumAge);
            Assert.Equal(18, (await read.ContentRestrictions.OfVersion(new(target, 2)).SingleAsync()).MinimumAge);
            using (read.SoftDelete.IncludeDeleted())
            {
                Assert.Single(await read.ContentRestrictions.OfKey(deleted).ToListAsync());
                Assert.Equal(1, await read.ContentAgeOptions.CountAsync());
            }
            Assert.Empty(await read.ContentAgeOptions.ToListAsync());
            Assert.Equal(7, await read.AuditLogEntries.CountAsync(item => item.EntityName == nameof(ContentRestriction)));
        }

        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Restore(id, new DefaultHttpContext(), context));
        }

        await using var restored = _factory.CreateContext();
        Assert.True(await restored.IsActiveContentAgeOption(18));
        Assert.Equal(18, (await restored.ContentRestrictions.OfVersion(new(target, 2)).SingleAsync()).MinimumAge);
        Assert.Equal(18, (await restored.ContentRestrictions.OfVersion(new(secondTarget, 1)).SingleAsync()).MinimumAge);
        Assert.Equal(6, await restored.ContentRestrictions.IgnoreQueryFilters().CountAsync());
        Assert.Contains(await restored.AuditLogEntries.ToListAsync(),
            item => item.EntityName == nameof(ContentAgeOption) && item.Action is AuditLogEntry.Restored);
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), context));
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.HardDelete(id, new DefaultHttpContext(), context));
        }
        await using var afterHardDelete = _factory.CreateContext();
        Assert.Equal(18, (await afterHardDelete.ContentRestrictions.OfVersion(new(target, 2)).SingleAsync()).MinimumAge);
        Assert.Equal(18, (await afterHardDelete.ContentRestrictions.OfVersion(new(secondTarget, 1)).SingleAsync()).MinimumAge);
        Assert.Equal(7, await afterHardDelete.AuditLogEntries.CountAsync(item => item.EntityName == nameof(ContentRestriction)));
    }

    [Fact]
    public async Task DeletedAgeCanBeRecreatedButCannotBeRestoredOverAnActiveCollision()
    {
        var original = await Create(16);
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(original, new DefaultHttpContext(), context));
        }
        var replacement = await Create(16);
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<Conflict>(await ContentAgeOptionEndpoints.Restore(original, new DefaultHttpContext(), context));
        }
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(replacement, new DefaultHttpContext(), context));
        }
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Restore(original, new DefaultHttpContext(), context));
        }
    }

    [Fact]
    public async Task HardDeleteRemovesEveryVersionOfDeletedOptionsOnlyAndRecordsAudit()
    {
        var id = await Create(18);
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            var version = new ContentAgeOption(new VersionedKey<Guid>(id, 2), 18, "18");
            context.ContentAgeOptions.Add(version);
            context.Entry(version).StampVersionedKey(id, 2);
            await context.SaveChangesAsync();
            Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.HardDelete(id, new DefaultHttpContext(), context));
        }
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), context));
        }
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.HardDelete(id, new DefaultHttpContext(), context));
        }
        await using var read = _factory.CreateContext();
        Assert.Empty(await read.ContentAgeOptions.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(2, await read.AuditLogEntries.CountAsync(
            item => item.EntityName == nameof(ContentAgeOption) && item.Action == new AuditLogEntry.HardDeleted()));
    }

    [Fact]
    public async Task MissingAndAlreadyDeletedOptionsReturnNotFound()
    {
        var id = await Create(18);
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.Restore(id, new DefaultHttpContext(), context));
            Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.Delete(Guid.NewGuid(), new DefaultHttpContext(), context));
            Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.Restore(Guid.NewGuid(), new DefaultHttpContext(), context));
            Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.HardDelete(Guid.NewGuid(), new DefaultHttpContext(), context));
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), context));
        }
        await using var deleted = _factory.CreateAuditedContext(Actor);
        Assert.IsType<NotFound>(await ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), deleted));
    }

    [Fact]
    public async Task DeletingOptionDoesNotInsertRestrictionVersions()
    {
        var id = await Create(18);
        var restriction = Guid.NewGuid();
        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            AddRestriction(context, restriction, 1, 18);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_unrestricted BEFORE INSERT ON ContentRestrictions
                WHEN NEW.MinimumAge = 0 BEGIN SELECT RAISE(ABORT, 'rejected'); END;
                """);
        }

        await using (var context = _factory.CreateAuditedContext(Actor))
        {
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(id, new DefaultHttpContext(), context));
        }

        await using var read = _factory.CreateContext();
        Assert.False(await read.IsActiveContentAgeOption(18));
        Assert.Equal(18, Assert.Single(await read.ContentRestrictions.ToListAsync()).MinimumAge);
        Assert.Equal(3, await read.AuditLogEntries.CountAsync());
    }

    private async Task<Guid> Create(int age)
    {
        await using var context = _factory.CreateAuditedContext(Actor);
        var result = Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
            await ContentAgeOptionEndpoints.Create(new(age, age.ToString()), new DefaultHttpContext(), context));
        Assert.Equal("/content-age-options", result.Location);
        Assert.Equal(age, result.Value!.Age);
        return result.Value.Id;
    }

    private static void AddRestriction(AbstractCoreContext context, Guid key, long version, int age)
    {
        var restriction = new ContentRestriction(new VersionedKey<Guid>(key, version), nameof(News), key, age);
        context.ContentRestrictions.Add(restriction);
        context.Entry(restriction).StampVersionedKey(key, version);
    }
}

using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class SiteCommandsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();
    private readonly AuditActor _actor;

    public SiteCommandsTests()
    {
        using var seed = _factory.CreateContext();
        var actorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "actor@test.com").GetAwaiter().GetResult();
        DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
        _actor = AuditActor.ForUser(actorId);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<SiteSaveResult> CreateAsync(string title, string slug, bool publish = false, string content = "<p>hi</p>")
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();
        return await new SiteCommands(ctx, versions, new FixedTimeProvider(Now)).Create(title, slug, content, publish);
    }

    private async Task<SiteSaveResult> SaveAsync(Guid id, string title, string slug, bool publish = false, string content = "<p>v</p>")
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();
        return await new SiteCommands(ctx, versions, new FixedTimeProvider(Now.AddMinutes(1))).SaveVersion(id, title, slug, content, publish);
    }

    private async Task<SiteSaveResult> ActivateAsync(VersionedKey<Guid> id)
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();
        return await new SiteCommands(ctx, versions, new FixedTimeProvider(Now.AddMinutes(2))).Activate(id);
    }

    private async Task<ISiteQueries.SiteHistory> HistoryAsync(Guid id)
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();
        return (await new SiteQueries(ctx, versions).FindHistory(id)).Match(() => throw new InvalidOperationException(), history => history);
    }

    private static VersionedKey<Guid> Saved(SiteSaveResult result) =>
        Assert.IsType<SiteSaveResult.Saved>(result).Id;

    [Fact]
    public async Task Create_Draft_StoresFirstVersionWithoutActivatingIt()
    {
        var id = Saved(await CreateAsync("About", "about"));

        var history = await HistoryAsync(id.Key);

        Assert.Equal(1L, id.Version);
        Assert.Equal(None, history.ActiveVersion);
        var version = Assert.Single(history.Versions);
        Assert.Equal(None, version.PublishedAt);
        Assert.False(version.IsActive);
        Assert.Equal(Now, version.CreatedAt);
    }

    [Fact]
    public async Task Create_Publish_PublishesAndActivatesTheFirstVersion()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));

        var history = await HistoryAsync(id.Key);

        Assert.Equal(Some(1L), history.ActiveVersion);
        var version = Assert.Single(history.Versions);
        Assert.Equal(Some(Now), version.PublishedAt);
        Assert.True(version.IsActive);
    }

    [Fact]
    public async Task SaveVersion_Draft_AddsAVersionAndKeepsTheActiveOne()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));

        var second = Saved(await SaveAsync(id.Key, "About us", "about"));

        var history = await HistoryAsync(id.Key);
        Assert.Equal(2L, second.Version);
        Assert.Equal(Some(1L), history.ActiveVersion);
        Assert.Equal([2L, 1L], history.Versions.Select(version => version.Version));
        Assert.Equal([false, true], history.Versions.Select(version => version.IsActive));
        Assert.Equal(None, history.Versions[0].PublishedAt);
    }

    [Fact]
    public async Task SaveVersion_Publish_SwitchesTheActiveVersion()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));

        await SaveAsync(id.Key, "About us", "about", publish: true);

        var history = await HistoryAsync(id.Key);
        Assert.Equal(Some(2L), history.ActiveVersion);
        Assert.Equal([true, false], history.Versions.Select(version => version.IsActive));
    }

    [Fact]
    public async Task SaveVersion_UnknownSite_ReturnsNotFound()
    {
        var result = await SaveAsync(Guid.NewGuid(), "Nope", "nope");

        Assert.IsType<SiteSaveResult.NotFound>(result);
    }

    [Fact]
    public async Task Create_SlugOfAnotherSitesLatestVersion_ReturnsSlugTaken()
    {
        await CreateAsync("About", "about");

        var result = await CreateAsync("Other", "about");

        Assert.IsType<SiteSaveResult.SlugTaken>(result);
    }

    [Fact]
    public async Task SaveVersion_KeepingItsOwnSlug_IsAllowed()
    {
        var id = Saved(await CreateAsync("About", "about"));

        var result = await SaveAsync(id.Key, "About", "about");

        Assert.IsType<SiteSaveResult.Saved>(result);
    }

    [Fact]
    public async Task SaveVersion_SlugOfAnotherSitesActiveVersion_ReturnsSlugTaken()
    {
        var first = Saved(await CreateAsync("About", "about", publish: true));
        await SaveAsync(first.Key, "About", "about-new");
        var other = Saved(await CreateAsync("Other", "other"));

        var result = await SaveAsync(other.Key, "Other", "about");

        Assert.IsType<SiteSaveResult.SlugTaken>(result);
    }

    [Fact]
    public async Task SaveVersion_SlugOnlyUsedByAnOldInactiveVersion_IsFree()
    {
        var first = Saved(await CreateAsync("About", "about", publish: true));
        await SaveAsync(first.Key, "About", "about-us", publish: true);
        await SaveAsync(first.Key, "About", "about-us-2", publish: true);

        var result = await CreateAsync("Other", "about");

        Assert.IsType<SiteSaveResult.Saved>(result);
    }

    [Fact]
    public async Task Activate_Draft_PublishesItAndMakesItActive()
    {
        var id = Saved(await CreateAsync("About", "about"));

        var result = await ActivateAsync(id);

        var history = await HistoryAsync(id.Key);
        Assert.Equal(id, Assert.IsType<SiteSaveResult.Saved>(result).Id);
        Assert.Equal(Some(1L), history.ActiveVersion);
        Assert.Equal(Some(Now.AddMinutes(2)), history.Versions[0].PublishedAt);
    }

    [Fact]
    public async Task Activate_PublishedOlderVersion_RollsBackWithoutChangingItsPublishDate()
    {
        var first = Saved(await CreateAsync("About", "about", publish: true));
        await SaveAsync(first.Key, "About", "about", publish: true);

        await ActivateAsync(first);

        var history = await HistoryAsync(first.Key);
        Assert.Equal(Some(1L), history.ActiveVersion);
        Assert.Equal(Some(Now), history.Versions[1].PublishedAt);
    }

    [Fact]
    public async Task Activate_ActiveVersionAgain_IsANoOp()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));

        var result = await ActivateAsync(id);

        Assert.IsType<SiteSaveResult.Saved>(result);
        Assert.Equal(Some(1L), (await HistoryAsync(id.Key)).ActiveVersion);
    }

    [Fact]
    public async Task Activate_UnknownVersion_ReturnsNotFound()
    {
        var id = Saved(await CreateAsync("About", "about"));

        var result = await ActivateAsync(new VersionedKey<Guid>(id.Key, 9));

        Assert.IsType<SiteSaveResult.NotFound>(result);
    }

    [Fact]
    public async Task Activate_VersionWhoseSlugIsNowTaken_ReturnsSlugTaken()
    {
        var first = Saved(await CreateAsync("About", "about", publish: true));
        await SaveAsync(first.Key, "About", "about-us", publish: true);
        await CreateAsync("Other", "about", publish: true);

        var result = await ActivateAsync(first);

        Assert.IsType<SiteSaveResult.SlugTaken>(result);
        Assert.Equal(Some(2L), (await HistoryAsync(first.Key)).ActiveVersion);
    }

    [Fact]
    public async Task Save_WritesCreatedAndActiveVersionAuditEntries()
    {
        Guid siteId;
        await using (var ctx = _factory.CreateAuditedContext(_actor))
        await using (var versions = _factory.CreateAuditedVersioningContext(_actor))
        {
            siteId = Saved(await new SiteCommands(ctx, versions, new FixedTimeProvider(Now)).Create("About", "about", "<p/>", true)).Key;
        }

        await using (var ctx = _factory.CreateAuditedContext(_actor))
        await using (var versions = _factory.CreateAuditedVersioningContext(_actor))
        {
            await new SiteCommands(ctx, versions, new FixedTimeProvider(Now)).SaveVersion(siteId, "About", "about", "<p/>", true);
        }

        await using var read = _factory.CreateContext();
        var entries = await read.AuditLogEntries.Select(entry => new { entry.EntityName, entry.EntityKey, entry.Action }).ToListAsync();

        Assert.Contains(entries, entry => entry.EntityName == "Site" && entry.EntityKey == $"{siteId}:1" && entry.Action is AuditLogEntry.Created);
        Assert.Contains(entries, entry => entry.EntityName == "Site" && entry.EntityKey == $"{siteId}:2" && entry.Action is AuditLogEntry.Created);
        Assert.Contains(entries, entry => entry.EntityName == "ActiveVersion" && entry.EntityKey == $"Site:{siteId}" && entry.Action is AuditLogEntry.Created);
        var updated = Assert.Single(entries, entry => entry.EntityName == "ActiveVersion" && entry.Action is AuditLogEntry.Updated);
        var change = Assert.Single(Assert.IsType<AuditLogEntry.Updated>(updated.Action).Differences);
        Assert.Equal("Version", change.PropertyName);
        Assert.Equal("1", change.OldValue?.ToString());
        Assert.Equal("2", change.NewValue?.ToString());
    }

    private async Task<Option<ISiteQueries.SiteHistory>> FindHistoryAsync(Guid id)
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();

        return await new SiteQueries(ctx, versions).FindHistory(id);
    }

    private async Task<bool> DeleteAsync(Guid id)
    {
        await using var ctx = _factory.CreateAuditedContext(_actor);
        await using var versions = _factory.CreateAuditedVersioningContext(_actor);
        return await new SiteCommands(ctx, versions, new FixedTimeProvider(Now)).Delete(id);
    }

    private async Task<SiteSaveResult> RestoreAsync(Guid id)
    {
        await using var ctx = _factory.CreateAuditedContext(_actor);
        await using var versions = _factory.CreateAuditedVersioningContext(_actor);
        return await new SiteCommands(ctx, versions, new FixedTimeProvider(Now)).Restore(id);
    }

    private async Task<HardDeleteResult> HardDeleteAsync(Guid id)
    {
        await using var ctx = _factory.CreateAuditedContext(_actor);
        await using var versions = _factory.CreateAuditedVersioningContext(_actor);
        return await new SiteCommands(ctx, versions, new FixedTimeProvider(Now)).HardDelete(id);
    }

    private async Task<(int Sites, int Active)> CountRowsAsync()
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();
        using var scope = ctx.SoftDelete.IncludeDeleted();

        return (await ctx.Sites.CountAsync(), await versions.ActiveVersions.CountAsync());
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersionButKeepsTheRows()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));
        await SaveAsync(id.Key, "About", "about");

        Assert.True(await DeleteAsync(id.Key));

        Assert.Equal((2, 1), await CountRowsAsync());
        Assert.Equal(None, await FindHistoryAsync(id.Key));
    }

    [Fact]
    public async Task Delete_UnknownOrAlreadyDeletedSite_ReturnsFalse()
    {
        var id = Saved(await CreateAsync("About", "about"));
        await DeleteAsync(id.Key);

        Assert.False(await DeleteAsync(id.Key));
        Assert.False(await DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_FreesTheSlug()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));
        await DeleteAsync(id.Key);

        Assert.IsType<SiteSaveResult.Saved>(await CreateAsync("Other", "about"));
    }

    [Fact]
    public async Task Restore_BringsEveryVersionAndTheLiveVersionBack()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));
        await SaveAsync(id.Key, "About", "about");
        await DeleteAsync(id.Key);

        var result = await RestoreAsync(id.Key);

        Assert.Equal(new VersionedKey<Guid>(id.Key, 2), Assert.IsType<SiteSaveResult.Saved>(result).Id);
        var history = await HistoryAsync(id.Key);
        Assert.Equal(Some(1L), history.ActiveVersion);
        Assert.Equal(2, history.Versions.Count);
    }

    [Fact]
    public async Task Restore_SiteThatIsNotDeleted_ReturnsNotFound()
    {
        var id = Saved(await CreateAsync("About", "about"));

        Assert.IsType<SiteSaveResult.NotFound>(await RestoreAsync(id.Key));
        Assert.IsType<SiteSaveResult.NotFound>(await RestoreAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Restore_WhenAnotherSiteTookTheSlug_ReturnsSlugTaken()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));
        await DeleteAsync(id.Key);
        await CreateAsync("Other", "about");

        Assert.IsType<SiteSaveResult.SlugTaken>(await RestoreAsync(id.Key));
    }

    [Fact]
    public async Task HardDelete_RemovesTheVersionsAndTheActiveVersionForGood()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));
        await SaveAsync(id.Key, "About", "about");
        await DeleteAsync(id.Key);

        Assert.Equal(HardDeleteResult.Deleted, await HardDeleteAsync(id.Key));

        Assert.Equal((0, 0), await CountRowsAsync());
    }

    [Fact]
    public async Task HardDelete_SiteThatIsNotDeleted_ReturnsNotFound()
    {
        var id = Saved(await CreateAsync("About", "about", publish: true));

        Assert.Equal(HardDeleteResult.NotFound, await HardDeleteAsync(id.Key));
        Assert.Equal(HardDeleteResult.NotFound, await HardDeleteAsync(Guid.NewGuid()));
        Assert.Equal((1, 1), await CountRowsAsync());
    }

    [Fact]
    public async Task Deletion_IsAuditedPerVersion()
    {
        Guid siteId;
        await using (var ctx = _factory.CreateAuditedContext(_actor))
        await using (var versions = _factory.CreateAuditedVersioningContext(_actor))
        {
            var commands = new SiteCommands(ctx, versions, new FixedTimeProvider(Now));
            siteId = Saved(await commands.Create("About", "about", "<p/>", true)).Key;
            await commands.SaveVersion(siteId, "About", "about", "<p/>", false);
        }

        foreach (var step in new[] { "delete", "restore", "delete", "hard" })
        {
            await using var ctx = _factory.CreateAuditedContext(_actor);
            await using var versions = _factory.CreateAuditedVersioningContext(_actor);
            var commands = new SiteCommands(ctx, versions, new FixedTimeProvider(Now));

            _ = step switch
            {
                "delete" => await commands.Delete(siteId),
                "restore" => await commands.Restore(siteId) is SiteSaveResult.Saved,
                _ => await commands.HardDelete(siteId) == HardDeleteResult.Deleted
            };
        }

        await using var read = _factory.CreateContext();
        var actions = (await read.AuditLogEntries
                .Where(entry => entry.EntityName == "Site" && entry.EntityKey == $"{siteId}:2")
                .OrderBy(entry => entry.Timestamp)
                .ToListAsync())
            .Select(entry => entry.Action.GetType().Name)
            .ToList();

        Assert.Equal(["Created", "Deleted", "Restored", "Deleted", "HardDeleted"], actions);
    }
}

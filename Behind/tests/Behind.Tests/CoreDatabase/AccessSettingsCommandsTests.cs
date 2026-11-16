using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Tests.Helpers.AccessSettingsTestBedExtensions;

namespace Behind.Tests.CoreDatabase;

public sealed class AccessSettingsCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<IReadOnlyList<(long Version, bool Enabled, string Message)>> VersionsAsync()
    {
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        var rows = await ctx.AccessSettings
            .OrderBy(settings => EF.Property<long>(settings, VersionedColumns.Version))
            .Select(settings => new
            {
                Version = EF.Property<long>(settings, VersionedColumns.Version),
                settings.RestrictionEnabled,
                settings.RestrictionMessage
            })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.RestrictionEnabled, row.RestrictionMessage)).ToList();
    }

    [Fact]
    public async Task Save_StoresVersionOneUnderTheSingletonKeyAndAuditsTheCreation()
    {
        var saved = await _bed.SaveAccessSettingsAsync();

        Assert.Equal(new VersionedKey<Guid>(AccessSettings.SingletonKey, 1), saved.Id);
        Assert.Equal([(1L, true, "Members only")], await VersionsAsync());
        Assert.Equal(["Created"], await _bed.ActionsAsync("AccessSettings", AccessSettings.SingletonKey));
    }

    [Fact]
    public async Task Save_MovesTheImagesOfTheMessageUnderTheSingletonParent()
    {
        var image = await _bed.AddVfsFileAsync(VfsTypes.AccessSettings, "logo.png");

        await _bed.SaveAccessSettingsAsync(AccessDraft(message: $"<p><img src=\"/access/images/{image}\"></p>"));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.AccessSettings, image, AccessSettings.SingletonKey)));
        Assert.False(await ExistsAsync(VfsTestPaths.Of(VfsTypes.AccessSettings, image)));
    }

    [Fact]
    public async Task Save_IgnoresImagesOfOtherKinds()
    {
        var foreign = await _bed.AddVfsFileAsync(VfsTypes.News, "other.png");

        await _bed.SaveAccessSettingsAsync(AccessDraft(message: $"<p><img src=\"/access/images/{foreign}\"></p>"));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.News, foreign)));
    }

    private async Task<bool> ExistsAsync(string path) => (await _bed.ContentStore.Read(path)).Match(() => false, _ => true);

    [Fact]
    public async Task Save_StoresTheTitleAndTheMessage()
    {
        await _bed.SaveAccessSettingsAsync(AccessDraft(message: "<p>Hi</p>") with { RestrictionTitle = "Private club" });

        var current = await _bed.WithAccessSettingsQueriesAsync(queries => queries.Current());

        Assert.Equal("Private club", current.RestrictionTitle);
        Assert.Equal("<p>Hi</p>", current.RestrictionMessage);
    }

    [Fact]
    public async Task Save_WithoutATitle_StoresAnEmptyTitleForTheDefault()
    {
        await _bed.SaveAccessSettingsAsync();

        Assert.Equal(
            string.Empty,
            (await _bed.WithAccessSettingsQueriesAsync(queries => queries.Current())).RestrictionTitle);
    }

    [Fact]
    public async Task Save_AgainInsertsTheNextVersionAndKeepsTheOldOne()
    {
        await _bed.SaveAccessSettingsAsync();

        var saved = await _bed.SaveAccessSettingsAsync(AccessDraft(false, message: "Closed"));

        Assert.Equal(2, saved.Id.Version);
        Assert.Equal([(1L, true, "Members only"), (2L, false, "Closed")], await VersionsAsync());
    }

    [Fact]
    public async Task Save_UnknownRole_IsRejectedAndStoresNothing()
    {
        await _bed.EnsureRolesAsync();

        var result = await _bed.WithAccessSettingsCommandsAsync(commands => commands.Save(AccessDraft(role: "Nobody")));

        Assert.IsType<AccessSettingsSaveResult.UnknownRole>(result);
        Assert.Empty(await VersionsAsync());
    }

    [Fact]
    public async Task Delete_SoftDeletesTheSettingsAndTurnsTheCurrentBackToOpen()
    {
        await _bed.SaveAccessSettingsAsync();

        var removed = await _bed.WithAccessSettingsCommandsAsync(commands => commands.Delete());

        Assert.True(removed);
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("AccessSettings", AccessSettings.SingletonKey));
        Assert.Equal(AccessSettings.Open, await _bed.WithAccessSettingsQueriesAsync(queries => queries.Current()));
    }

    [Fact]
    public async Task Delete_WithoutSavedSettings_IsNotFound()
    {
        Assert.False(await _bed.WithAccessSettingsCommandsAsync(commands => commands.Delete()));
    }

    [Fact]
    public async Task Save_AfterADeleteStartsAnActiveVersionAboveTheDeletedOne()
    {
        await _bed.SaveAccessSettingsAsync();
        await _bed.WithAccessSettingsCommandsAsync(commands => commands.Delete());

        var saved = await _bed.SaveAccessSettingsAsync(AccessDraft(message: "Again"));

        Assert.Equal(2, saved.Id.Version);
        Assert.Equal(
            "Again",
            (await _bed.WithAccessSettingsQueriesAsync(queries => queries.Current())).RestrictionMessage);
        Assert.Equal(
            None,
            await _bed.WithAccessSettingsQueriesAsync(queries => queries.FindDeleted()));
    }

    [Fact]
    public async Task Restore_BringsTheDeletedSettingsBack()
    {
        await _bed.SaveAccessSettingsAsync();
        await _bed.WithAccessSettingsCommandsAsync(commands => commands.Delete());

        var result = await _bed.WithAccessSettingsCommandsAsync(commands => commands.Restore());

        Assert.Equal(
            new VersionedKey<Guid>(AccessSettings.SingletonKey, 1),
            Assert.IsType<AccessSettingsSaveResult.Saved>(result).Id);
        Assert.Equal(
            ["Created", "Deleted", "Restored"],
            await _bed.ActionsAsync("AccessSettings", AccessSettings.SingletonKey));
        Assert.True((await _bed.WithAccessSettingsQueriesAsync(queries => queries.Current())).RestrictionEnabled);
    }

    [Fact]
    public async Task Restore_SettingsThatAreNotDeleted_IsNotFound()
    {
        await _bed.SaveAccessSettingsAsync();

        Assert.IsType<AccessSettingsSaveResult.NotFound>(
            await _bed.WithAccessSettingsCommandsAsync(commands => commands.Restore()));
    }

    [Fact]
    public async Task HardDelete_RemovesDeletedSettingsForGood()
    {
        await _bed.SaveAccessSettingsAsync();
        await _bed.WithAccessSettingsCommandsAsync(commands => commands.Delete());

        var result = await _bed.WithAccessSettingsCommandsAsync(commands => commands.HardDelete());

        Assert.Equal(HardDeleteResult.Deleted, result);
        Assert.Empty(await VersionsAsync());
    }

    [Fact]
    public async Task HardDelete_SettingsThatAreNotDeleted_IsNotFound()
    {
        await _bed.SaveAccessSettingsAsync();

        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithAccessSettingsCommandsAsync(commands => commands.HardDelete()));
    }
}

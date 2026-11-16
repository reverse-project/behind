using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class UserSettingTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUserAsync(string email)
    {
        await using var seed = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(seed, email);
    }

    private async Task SetAsync(Guid userId, string key, string value)
    {
        await using var context = _factory.CreateContext();
        await new UserSettingCommands(context).Set(userId, key, value);
    }

    private async Task<IReadOnlyDictionary<string, string>> AllOf(Guid userId)
    {
        await using var context = _factory.CreateContext();
        return await new UserSettingQueries(context).GetAll(userId);
    }

    [Fact]
    public async Task GetAll_UserWithoutSettings_ReturnsNothing()
    {
        var userId = await SeedUserAsync("none@test.com");

        Assert.Empty(await AllOf(userId));
    }

    [Fact]
    public async Task Set_NewKey_StoresTheValue()
    {
        var userId = await SeedUserAsync("new@test.com");

        await SetAsync(userId, UserSettingKeys.Locale, "hu-HU");

        Assert.Equal("hu-HU", (await AllOf(userId))[UserSettingKeys.Locale]);
    }

    [Fact]
    public async Task Set_ExistingKey_ReplacesTheValue()
    {
        var userId = await SeedUserAsync("replace@test.com");
        await SetAsync(userId, UserSettingKeys.Locale, "hu-HU");

        await SetAsync(userId, UserSettingKeys.Locale, "en-US");

        var settings = await AllOf(userId);
        Assert.Equal("en-US", Assert.Single(settings).Value);
    }

    [Fact]
    public async Task Set_EmptyValue_RemovesTheKey()
    {
        var userId = await SeedUserAsync("remove@test.com");
        await SetAsync(userId, UserSettingKeys.Locale, "hu-HU");

        await SetAsync(userId, UserSettingKeys.Locale, string.Empty);

        Assert.Empty(await AllOf(userId));
    }

    [Fact]
    public async Task Set_EmptyValueForAMissingKey_StoresNothing()
    {
        var userId = await SeedUserAsync("missing@test.com");

        await SetAsync(userId, UserSettingKeys.Locale, string.Empty);

        Assert.Empty(await AllOf(userId));
    }

    [Fact]
    public async Task Set_KeepsTheSettingsOfEachUserApart()
    {
        var ada = await SeedUserAsync("ada@test.com");
        var bob = await SeedUserAsync("bob@test.com");

        await SetAsync(ada, UserSettingKeys.Locale, "hu-HU");
        await SetAsync(bob, UserSettingKeys.Locale, "en-US");

        Assert.Equal("hu-HU", (await AllOf(ada))[UserSettingKeys.Locale]);
        Assert.Equal("en-US", (await AllOf(bob))[UserSettingKeys.Locale]);
    }

    [Fact]
    public async Task HardDeletingTheUser_RemovesItsSettings()
    {
        var userId = await SeedUserAsync("gone@test.com");
        await SetAsync(userId, UserSettingKeys.Locale, "hu-HU");

        await using (var context = _factory.CreateContext())
        {
            Assert.True(await new UserCommands(context).HardDelete(userId));
        }

        await using var reader = _factory.CreateContext();
        Assert.False(await reader.Set<Dictionary<string, object>>(UserSettingStore.Table).AnyAsync());
    }

    [Fact]
    public void Known_ListsTheLocaleKey()
    {
        Assert.Contains(UserSettingKeys.Locale, UserSettingKeys.Known);
        Assert.DoesNotContain("unknown", UserSettingKeys.Known);
    }
}

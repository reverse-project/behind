using Behind.Core.Database;
using static Behind.Tests.Helpers.AccessSettingsTestBedExtensions;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class AccessSettingsQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task Current_WithoutSavedSettings_IsOpenToEveryone()
    {
        var current = await _bed.WithAccessSettingsQueriesAsync(queries => queries.Current());

        Assert.False(current.RestrictionEnabled);
        Assert.False(current.ModeratedSignUp);
        Assert.Equal(DomainConstants.UsersRole, current.RestrictedToRole);
    }

    [Fact]
    public async Task Current_ReturnsTheLatestVersion()
    {
        await _bed.SaveAccessSettingsAsync(AccessDraft(message: "First"));
        await _bed.SaveAccessSettingsAsync(AccessDraft(message: "Second", moderatedSignUp: true));

        var current = await _bed.WithAccessSettingsQueriesAsync(queries => queries.Current());

        Assert.Equal(new VersionedKey<Guid>(AccessSettings.SingletonKey, 2), current.Id);
        Assert.Equal("Second", current.RestrictionMessage);
        Assert.True(current.ModeratedSignUp);
        Assert.True(current.RestrictionEnabled);
        Assert.Equal("Users", current.RestrictedToRole);
    }

    [Fact]
    public async Task FindDeleted_ReturnsTheLatestDeletedVersion()
    {
        await _bed.SaveAccessSettingsAsync(AccessDraft(message: "First"));
        await _bed.SaveAccessSettingsAsync(AccessDraft(message: "Second"));
        await _bed.WithAccessSettingsCommandsAsync(commands => commands.Delete());

        var deleted = await _bed.WithAccessSettingsQueriesAsync(queries => queries.FindDeleted());

        Assert.Equal("Second", deleted.Match(() => string.Empty, settings => settings.RestrictionMessage));
    }

    [Fact]
    public async Task FindDeleted_WithoutDeletedSettings_IsNothing()
    {
        await _bed.SaveAccessSettingsAsync();

        Assert.Equal(None, await _bed.WithAccessSettingsQueriesAsync(queries => queries.FindDeleted()));
    }
}

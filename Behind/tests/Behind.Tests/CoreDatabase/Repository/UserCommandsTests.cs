using Behind.Core.Database.Commands;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class UserCommandsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUserAsync(string email)
    {
        await using var seed = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(seed, email);
    }

    private async Task<string> DisplayNameOf(Guid userId)
    {
        await using var reader = _factory.CreateContext();
        return await reader.Users
            .Where(user => EF.Property<Guid>(user, "Id") == userId)
            .Select(user => user.DisplayName)
            .SingleAsync();
    }

    [Fact]
    public async Task ChangeDisplayName_ExistingUser_PersistsTrimmedName()
    {
        var userId = await SeedUserAsync("rename@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new UserCommands(ctx).ChangeDisplayName(userId, "  Ada  "));
        }

        Assert.Equal("Ada", await DisplayNameOf(userId));
    }

    [Fact]
    public async Task ChangeDisplayName_WhitespaceName_PersistsEmptyName()
    {
        var userId = await SeedUserAsync("blank@test.com");

        await using (var ctx = _factory.CreateContext())
        {
            Assert.True(await new UserCommands(ctx).ChangeDisplayName(userId, "   "));
        }

        Assert.Equal(string.Empty, await DisplayNameOf(userId));
    }

    [Fact]
    public async Task ChangeDisplayName_UnknownUser_ReturnsFalse()
    {
        await using var ctx = _factory.CreateContext();

        Assert.False(await new UserCommands(ctx).ChangeDisplayName(Guid.NewGuid(), "Ada"));
    }
}

using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Commands;

public sealed class EmailAddressCommandsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> User(string email = "owner@test.com")
    {
        await using var context = _factory.CreateContext();
        return await DatabaseSeedHelper.SeedHumanUserAsync(context, email);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Add_PersistsNormalizedAddressAndConfiguredConfirmation(bool required)
    {
        var userId = await User();
        await using (var context = _factory.CreateContext())
        {
            var result = await new EmailCommands(context, new FixedTimeProvider(Now))
                .SaveAddress(userId, null, " Second@Test.com ", required);
            Assert.Equal(IEmailCommands.AddressStatus.Saved, result.Status);
            Assert.Equal("second@test.com", result.Address!.Value);
            Assert.Equal(required ? null : (DateTimeOffset?)Now, result.Address.ConfirmedAt);
        }

        await using var read = _factory.CreateContext();
        var addresses = await new EmailQueries(read).Addresses(userId);
        Assert.Equal(2, addresses.Count);
        Assert.Equal("owner@test.com", addresses[0].Value);
        Assert.Equal("second@test.com", addresses[1].Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Edit_ResetsOnlyTheEditedAddressAndPreservesItsIdentity(bool required)
    {
        var userId = await User();
        Guid id;
        await using (var context = _factory.CreateContext())
        {
            var commands = new EmailCommands(context, new FixedTimeProvider(Now));
            var first = Assert.Single(await new EmailQueries(context).Addresses(userId));
            id = first.Id;
            await commands.ConfirmAddress(userId, id, first.Value);
            await commands.SaveAddress(userId, null, "second@test.com", false);
        }

        await using (var context = _factory.CreateAuditedContext(AuditActor.ForUser(userId)))
        {
            var result = await new EmailCommands(context, new FixedTimeProvider(Now.AddDays(1)))
                .SaveAddress(userId, id, "Replacement@test.com", required);
            Assert.Equal(id, result.Address!.Id);
            Assert.Equal(required ? null : (DateTimeOffset?)Now.AddDays(1), result.Address.ConfirmedAt);
        }

        await using var read = _factory.CreateContext();
        var addresses = await new EmailQueries(read).Addresses(userId);
        Assert.Equal("replacement@test.com", addresses[0].Value);
        Assert.Equal(Now, addresses[1].ConfirmedAt);
        Assert.Equal(userId, await read.Emails.Where(email => EF.Property<Guid>(email, "Id") == id)
            .Select(email => EF.Property<Guid>(email, "UserId")).SingleAsync());
        var updated = Assert.IsType<AuditLogEntry.Updated>(Assert.Single(await read.AuditLogEntries.ToListAsync()).Action);
        Assert.Contains(updated.Differences, difference => difference.PropertyName == nameof(Email.Value));
        var labels = await read.ResolveAuditLabelsAsync([id], []);
        Assert.Equal("replacement@test.com", labels.Values[id]);
    }

    [Fact]
    public async Task UnchangedAddress_KeepsExistingConfirmation()
    {
        var userId = await User();
        await using var context = _factory.CreateContext();
        var commands = new EmailCommands(context, new FixedTimeProvider(Now));
        var address = Assert.Single(await new EmailQueries(context).Addresses(userId));
        await commands.ConfirmAddress(userId, address.Id, address.Value);
        context.ChangeTracker.Clear();
        var result = await commands.SaveAddress(userId, address.Id, "OWNER@Test.com", true);
        Assert.Equal(Now, result.Address!.ConfirmedAt);
    }

    [Fact]
    public async Task DuplicateAddresses_AreRejectedWithinAndAcrossAccounts()
    {
        var userId = await User();
        await User("other@test.com");
        await User("Legacy@TEST.com");
        await using var context = _factory.CreateContext();
        var commands = new EmailCommands(context, TimeProvider.System);
        foreach (var value in new[] { "OWNER@test.com", "OTHER@test.com", "legacy@test.com" })
        {
            Assert.Equal(IEmailCommands.AddressStatus.InUse,
                (await commands.SaveAddress(userId, null, value, true)).Status);
        }

        Assert.Single(await new EmailQueries(context).Addresses(userId));
    }

    [Fact]
    public async Task EditRemoveAndConfirm_CannotTouchAnotherUsersAddress()
    {
        var userId = await User();
        var otherId = await User("other@test.com");
        await using var context = _factory.CreateContext();
        var address = Assert.Single(await new EmailQueries(context).Addresses(otherId));
        var commands = new EmailCommands(context, TimeProvider.System);
        Assert.Equal(IEmailCommands.AddressStatus.NotFound,
            (await commands.SaveAddress(userId, address.Id, "changed@test.com", true)).Status);
        Assert.Equal(IEmailCommands.AddressStatus.NotFound, await commands.RemoveAddress(userId, address.Id));
        Assert.False(await commands.ConfirmAddress(userId, address.Id, address.Value));
        Assert.Null(Assert.Single(await new EmailQueries(context).Addresses(otherId)).ConfirmedAt);
    }

    [Fact]
    public async Task Remove_RetainsAtLeastOneAddress()
    {
        var userId = await User();
        await using var context = _factory.CreateContext();
        var commands = new EmailCommands(context, TimeProvider.System);
        var first = Assert.Single(await new EmailQueries(context).Addresses(userId));
        Assert.Equal(IEmailCommands.AddressStatus.LastAddress, await commands.RemoveAddress(userId, first.Id));
        var second = await commands.SaveAddress(userId, null, "second@test.com", true);
        Assert.Equal(IEmailCommands.AddressStatus.Saved, await commands.RemoveAddress(userId, second.Address!.Id));
        Assert.Equal(IEmailCommands.AddressStatus.LastAddress, await commands.RemoveAddress(userId, first.Id));
    }

    [Fact]
    public async Task Confirmation_RejectsReplacedOrRemovedAddressAndKeepsFirstTimestamp()
    {
        var userId = await User();
        await using var context = _factory.CreateContext();
        var commands = new EmailCommands(context, new FixedTimeProvider(Now));
        var added = (await commands.SaveAddress(userId, null, "second@test.com", true)).Address!;
        await commands.SaveAddress(userId, added.Id, "changed@test.com", true);
        Assert.False(await commands.ConfirmAddress(userId, added.Id, added.Value));
        Assert.True(await commands.ConfirmAddress(userId, added.Id, "changed@test.com"));
        context.ChangeTracker.Clear();
        Assert.True(await new EmailCommands(context, new FixedTimeProvider(Now.AddDays(1)))
            .ConfirmAddress(userId, added.Id, "changed@test.com"));
        Assert.Equal(Now, (await new EmailQueries(context).Addresses(userId))[1].ConfirmedAt);
        await commands.RemoveAddress(userId, added.Id);
        Assert.False(await commands.ConfirmAddress(userId, added.Id, "changed@test.com"));
    }
}

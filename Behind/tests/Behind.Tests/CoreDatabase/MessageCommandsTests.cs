using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Tests.Helpers.MessageTestBedExtensions;

namespace Behind.Tests.CoreDatabase;

public sealed class MessageCommandsTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 8, 0, 0, TimeSpan.FromHours(1));

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<IReadOnlyList<(long Version, Message Message)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.Messages
            .OfKey(key)
            .OrderBy(message => EF.Property<long>(message, VersionedColumns.Version))
            .Select(message => new { Version = EF.Property<long>(message, VersionedColumns.Version), Message = message })
            .AsNoTracking()
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Message)).ToList();
    }

    [Fact]
    public async Task Create_StoresVersionOneWithTheScheduleAndAuditsTheCreation()
    {
        var key = await _bed.CreateMessageAsync(MessageDraft(
            " maintenance ",
            " Maintenance ",
            "<p>Down</p>",
            MessageLevel.Danger,
            Start,
            Start.AddHours(2)));

        var stored = Assert.Single(await VersionsAsync(key)).Message;
        Assert.Equal(
            ("maintenance", "Maintenance", "<p>Down</p>", MessageLevel.Danger),
            (stored.Name, stored.Title, stored.Content, stored.Level));
        Assert.Equal(Start, stored.StartsAt);
        Assert.Equal(Start.AddHours(2), stored.EndsAt);
        Assert.Equal(TimeSpan.Zero, stored.StartsAt!.Value.Offset);
        Assert.Equal(["Created"], await _bed.ActionsAsync("Message", key));
    }

    [Fact]
    public async Task Create_WithoutSchedule_StoresNullBounds()
    {
        var stored = Assert.Single(await VersionsAsync(await _bed.CreateMessageAsync())).Message;

        Assert.Null(stored.StartsAt);
        Assert.Null(stored.EndsAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_EndNotAfterStart_IsRejectedAsAnInvalidSchedule(int hours)
    {
        var result = await _bed.WithMessageCommandsAsync(commands =>
            commands.Create(MessageDraft(startsAt: Start, endsAt: Start.AddHours(hours))));

        Assert.Equal(MessageCommands.InvalidScheduleReason, Assert.IsType<VersionedSaveResult.Rejected>(result).Reason);
    }

    [Fact]
    public async Task Create_BlankName_IsRejectedAsInvalid()
    {
        var result = await _bed.WithMessageCommandsAsync(commands => commands.Create(MessageDraft(name: "  ")));

        Assert.Equal(MessageCommands.InvalidReason, Assert.IsType<VersionedSaveResult.Rejected>(result).Reason);
    }

    [Fact]
    public async Task Create_UndefinedLevel_IsRejectedAsInvalid()
    {
        var result = await _bed.WithMessageCommandsAsync(commands =>
            commands.Create(MessageDraft(level: (MessageLevel)42)));

        Assert.Equal(MessageCommands.InvalidReason, Assert.IsType<VersionedSaveResult.Rejected>(result).Reason);
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionAndKeepsTheOldOne()
    {
        var key = await _bed.CreateMessageAsync(MessageDraft("first"));

        var result = await _bed.WithMessageCommandsAsync(commands =>
            commands.Update(key, MessageDraft("second", level: MessageLevel.Success)));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<VersionedSaveResult.Saved>(result).Id);
        Assert.Equal(
            [(1L, "first", MessageLevel.Warning), (2L, "second", MessageLevel.Success)],
            (await VersionsAsync(key)).Select(row => (row.Version, row.Message.Name, row.Message.Level)));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Message", key, 2));
    }

    [Fact]
    public async Task Update_UnknownMessage_IsNotFound()
    {
        Assert.IsType<VersionedSaveResult.NotFound>(
            await _bed.WithMessageCommandsAsync(commands => commands.Update(Guid.NewGuid(), MessageDraft())));
    }

    [Fact]
    public async Task Update_DeletedMessage_IsNotFound()
    {
        var key = await _bed.CreateMessageAsync();
        await _bed.WithMessageCommandsAsync(commands => commands.Delete(key));

        Assert.IsType<VersionedSaveResult.NotFound>(
            await _bed.WithMessageCommandsAsync(commands => commands.Update(key, MessageDraft())));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersion()
    {
        var key = await _bed.CreateMessageAsync();
        await _bed.WithMessageCommandsAsync(commands => commands.Update(key, MessageDraft("again")));

        Assert.True(await _bed.WithMessageCommandsAsync(commands => commands.Delete(key)));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Message", key));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Message", key, 2));
    }

    [Fact]
    public async Task Delete_UnknownMessage_IsNotFound()
    {
        Assert.False(await _bed.WithMessageCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsTheMessageBack()
    {
        var key = await _bed.CreateMessageAsync();
        await _bed.WithMessageCommandsAsync(commands => commands.Delete(key));

        Assert.True(await _bed.WithMessageCommandsAsync(commands => commands.Restore(key)));
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("Message", key));
    }

    [Fact]
    public async Task Restore_MessageThatIsNotDeleted_IsNotFound()
    {
        var key = await _bed.CreateMessageAsync();

        Assert.False(await _bed.WithMessageCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task HardDelete_RemovesADeletedMessageForGood()
    {
        var key = await _bed.CreateMessageAsync();
        await _bed.WithMessageCommandsAsync(commands => commands.Delete(key));

        Assert.Equal(HardDeleteResult.Deleted, await _bed.WithMessageCommandsAsync(commands => commands.HardDelete(key)));
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        Assert.False(await ctx.Messages.OfKey(key).AnyAsync());
    }

    [Fact]
    public async Task HardDelete_MessageThatIsNotDeleted_IsNotFound()
    {
        var key = await _bed.CreateMessageAsync();

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithMessageCommandsAsync(commands => commands.HardDelete(key)));
    }
}

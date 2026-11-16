using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.WebApi.Scheduling;
using Microsoft.EntityFrameworkCore;
using static Behind.Tests.Helpers.ScheduleTestBedExtensions;

namespace Behind.Tests.CoreDatabase;

public sealed class ScheduleCommandsTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<IReadOnlyList<Schedule>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();

        return await ctx.Schedules
            .OfKey(key)
            .OrderBy(schedule => EF.Property<long>(schedule, VersionedColumns.Version))
            .AsNoTracking()
            .ToListAsync();
    }

    [Fact]
    public async Task Create_StoresTheTrimmedScheduleWithNormalizedParticipantsAndAuditsIt()
    {
        var key = await _bed.CreateScheduleAsync(ScheduleDraft(
            " Nightly ",
            "*/5 * * * *",
            parameters: new Dictionary<string, string> { ["graph"] = "g1", ["connector"] = "c1" },
            notify: ScheduleNotification.Failure,
            participants:
            [
                new(ScheduleParticipantKind.Role, RoleId.ToString("N")),
                new(ScheduleParticipantKind.Email, " ops@example.test "),
                new(ScheduleParticipantKind.Email, "OPS@example.test")
            ]));

        var stored = Assert.Single(await VersionsAsync(key));
        Assert.Equal(("Nightly", "*/5 * * * *", TestAction, ScheduleNotification.Failure), (stored.Name, stored.Cron.Text, stored.Action, stored.Notify));
        Assert.Equal("c1", stored.Parameters.Values["connector"]);
        Assert.Equal(
            [new ScheduleParticipant(ScheduleParticipantKind.Role, RoleId.ToString()), new ScheduleParticipant(ScheduleParticipantKind.Email, "ops@example.test")],
            stored.Participants.Items);
        Assert.Equal(["Created"], await _bed.ActionsAsync("Schedule", key));
    }

    [Fact]
    public async Task Update_AddsANewVersion()
    {
        var key = await _bed.CreateScheduleAsync();

        var result = await _bed.WithScheduleCommandsAsync(commands =>
            commands.Update(key, ScheduleDraft("Renamed", "0 3 * * *", enabled: false)));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<VersionedSaveResult.Saved>(result).Id);
        var latest = (await VersionsAsync(key)).Last();
        Assert.Equal(("Renamed", "0 3 * * *", false), (latest.Name, latest.Cron.Text, latest.Enabled));
    }

    [Fact]
    public async Task Update_WithStaleExpectedVersion_IsAVersionConflictWithoutSaving()
    {
        var key = await _bed.CreateScheduleAsync();
        await _bed.WithScheduleCommandsAsync(commands => commands.Update(key, 1, ScheduleDraft("First")));

        var stale = await _bed.WithScheduleCommandsAsync(commands => commands.Update(key, 1, ScheduleDraft("Stale")));
        var ahead = await _bed.WithScheduleCommandsAsync(commands => commands.Update(key, 5, ScheduleDraft("Ahead")));
        var current = await _bed.WithScheduleCommandsAsync(commands => commands.Update(key, 2, ScheduleDraft("Current")));

        Assert.IsType<VersionedSaveResult.VersionConflict>(stale);
        Assert.IsType<VersionedSaveResult.VersionConflict>(ahead);
        Assert.Equal(new VersionedKey<Guid>(key, 3), Assert.IsType<VersionedSaveResult.Saved>(current).Id);
        Assert.Equal(["First", "Current"], (await VersionsAsync(key)).Skip(1).Select(schedule => schedule.Name));
    }

    [Fact]
    public async Task Update_WithExpectedVersionOfAMissingSchedule_IsNotFound()
    {
        var result = await _bed.WithScheduleCommandsAsync(commands => commands.Update(Guid.CreateVersion7(), 1, ScheduleDraft("Missing")));

        Assert.IsType<VersionedSaveResult.NotFound>(result);
    }

    [Theory]
    [InlineData("  ", TestAction)]
    [InlineData("Name", " ")]
    public async Task Create_BlankNameOrAction_IsRejectedAsInvalid(string name, string action)
    {
        var result = await _bed.WithScheduleCommandsAsync(commands => commands.Create(ScheduleDraft(name, action: action)));

        Assert.Equal(ScheduleCommands.InvalidReason, Assert.IsType<VersionedSaveResult.Rejected>(result).Reason);
    }

    [Fact]
    public async Task Create_TooLongName_IsRejectedAsInvalid()
    {
        var result = await _bed.WithScheduleCommandsAsync(commands =>
            commands.Create(ScheduleDraft(new string('a', Schedule.MaxNameLength + 1))));

        Assert.Equal(ScheduleCommands.InvalidReason, Assert.IsType<VersionedSaveResult.Rejected>(result).Reason);
    }

    [Theory]
    [InlineData(ScheduleParticipantKind.Email, "nope")]
    [InlineData(ScheduleParticipantKind.User, "not-a-guid")]
    [InlineData(ScheduleParticipantKind.Role, "00000000-0000-0000-0000-000000000000")]
    public async Task Create_InvalidParticipant_IsRejected(ScheduleParticipantKind kind, string value)
    {
        var result = await _bed.WithScheduleCommandsAsync(commands =>
            commands.Create(ScheduleDraft(participants: [new(kind, value)])));

        Assert.Equal(ScheduleCommands.InvalidParticipantsReason, Assert.IsType<VersionedSaveResult.Rejected>(result).Reason);
    }

    [Fact]
    public async Task DeleteAndRestore_ToggleVisibility()
    {
        var key = await _bed.CreateScheduleAsync();

        await _bed.WithScheduleCommandsAsync(commands => commands.Delete(key));
        Assert.True((await _bed.WithScheduleQueriesAsync(queries => queries.Find(key, DeletionState.Active))).IsNone());
        Assert.False((await _bed.WithScheduleQueriesAsync(queries => queries.Find(key, DeletionState.Deleted))).IsNone());

        await _bed.WithScheduleCommandsAsync(commands => commands.Restore(key));
        Assert.False((await _bed.WithScheduleQueriesAsync(queries => queries.Find(key, DeletionState.Active))).IsNone());
    }

    [Fact]
    public async Task HardDelete_RemovesADeletedScheduleWithoutRuns()
    {
        var key = await _bed.CreateScheduleAsync();
        await _bed.WithScheduleCommandsAsync(commands => commands.Delete(key));

        Assert.Equal(HardDeleteResult.Deleted, await _bed.WithScheduleCommandsAsync(commands => commands.HardDelete(key)));
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        Assert.False(await ctx.Schedules.OfKey(key).AnyAsync());
    }

    [Fact]
    public async Task HardDelete_ScheduleWithRuns_IsRejectedEvenWhenTheRunsAreDeleted()
    {
        var key = await _bed.CreateScheduleAsync();
        var run = await _bed.CreateScheduleRunAsync(ScheduleRunDraft(key, Start));
        await _bed.WithScheduleRunCommandsAsync(commands => commands.Delete(run));
        await _bed.WithScheduleCommandsAsync(commands => commands.Delete(key));

        Assert.Equal(HardDeleteResult.Rejected, await _bed.WithScheduleCommandsAsync(commands => commands.HardDelete(key)));

        await _bed.WithScheduleRunCommandsAsync(commands => commands.HardDelete(run));
        Assert.Equal(HardDeleteResult.Deleted, await _bed.WithScheduleCommandsAsync(commands => commands.HardDelete(key)));
    }

    [Fact]
    public async Task RunCreate_TruncatesLongTextsAndUpdateAddsAVersion()
    {
        var key = await _bed.CreateScheduleAsync();
        var run = await _bed.CreateScheduleRunAsync(ScheduleRunDraft(
            key, Start, status: ScheduleRunStatus.Running, summary: new string('x', ScheduleRun.MaxSummaryLength + 50)));

        var stored = Assert.Single(await _bed.WithScheduleRunQueriesAsync(queries => queries.Running()));
        Assert.Equal(ScheduleRun.MaxSummaryLength, stored.Summary.Length);

        var finished = stored.ToDraft() with { Status = ScheduleRunStatus.Failed, FinishedAt = Start.AddMinutes(2) };
        var result = await _bed.WithScheduleRunCommandsAsync(commands => commands.Update(run, finished));

        Assert.Equal(2, Assert.IsType<VersionedSaveResult.Saved>(result).Id.Version);
        Assert.Empty(await _bed.WithScheduleRunQueriesAsync(queries => queries.Running()));
        Assert.Equal(["Created"], await _bed.ActionsAsync("ScheduleRun", run));
    }
}

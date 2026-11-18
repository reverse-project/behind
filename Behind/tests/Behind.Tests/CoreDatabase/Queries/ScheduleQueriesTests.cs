using Behind.Core.Database;
using static Behind.Tests.Helpers.ScheduleTestBedExtensions;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class ScheduleQueriesTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static IReadOnlyList<string> Names(PagedResult<IScheduleQueries.ScheduleListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Name).ToList());

    private Task<PagedResult<IScheduleQueries.ScheduleListing>> ListAsync(
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active) =>
        _bed.WithScheduleQueriesAsync(queries => queries.List(new Flipper(1, 10), search, letter, state));

    [Fact]
    public async Task List_ReturnsTheLatestVersionsOrderedByNameAndSearchesNameAndAction()
    {
        var weekly = await _bed.CreateScheduleAsync(ScheduleDraft("Weekly"));
        await _bed.WithScheduleCommandsAsync(commands => commands.Update(weekly, ScheduleDraft("Weekly", "0 0 * * mon", action: "auditor.audit")));
        await _bed.CreateScheduleAsync(ScheduleDraft("Daily"));

        var page = await ListAsync();

        Assert.Equal(["Daily", "Weekly"], Names(page));
        var listing = page.Results.GetOrElse([]).Last();
        Assert.Equal((new VersionedKey<Guid>(weekly, 2), "0 0 * * mon"), (listing.Id, listing.Cron.Text));
        Assert.Equal(["Weekly"], Names(await ListAsync(Some("AUDITOR"))));
        Assert.Equal(["Daily"], Names(await ListAsync(letter: Some("D"))));
        Assert.Equal(["D", "W"], await _bed.WithScheduleQueriesAsync(queries => queries.GetLetters(None, DeletionState.Active)));
    }

    [Fact]
    public async Task List_SeparatesActiveAndDeletedSchedules()
    {
        await _bed.CreateScheduleAsync(ScheduleDraft("Kept"));
        var gone = await _bed.CreateScheduleAsync(ScheduleDraft("Gone"));
        await _bed.WithScheduleCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["Kept"], Names(await ListAsync()));
        Assert.Equal(["Gone"], Names(await ListAsync(state: DeletionState.Deleted)));
    }

    [Fact]
    public async Task Enabled_ReturnsOnlyActiveEnabledSchedulesWithDetails()
    {
        await _bed.CreateScheduleAsync(ScheduleDraft("On", parameters: new Dictionary<string, string> { ["graph"] = "g" }));
        await _bed.CreateScheduleAsync(ScheduleDraft("Off", enabled: false));
        var gone = await _bed.CreateScheduleAsync(ScheduleDraft("Gone"));
        await _bed.WithScheduleCommandsAsync(commands => commands.Delete(gone));

        var enabled = Assert.Single(await _bed.WithScheduleQueriesAsync(queries => queries.Enabled()));

        Assert.Equal("On", enabled.Name);
        Assert.Equal("g", enabled.Parameters.Values["graph"]);
    }

    [Fact]
    public async Task Runs_AreListedNewestFirstAndFilterByScheduleAndStatus()
    {
        var first = await _bed.CreateScheduleAsync(ScheduleDraft("First"));
        var second = await _bed.CreateScheduleAsync(ScheduleDraft("Second"));
        var old = await _bed.CreateScheduleRunAsync(ScheduleRunDraft(first, Start, "First"));
        var failed = await _bed.CreateScheduleRunAsync(ScheduleRunDraft(first, Start.AddHours(1), "First", ScheduleRunStatus.Failed));
        var other = await _bed.CreateScheduleRunAsync(ScheduleRunDraft(second, Start.AddHours(2), "Second"));
        var deleted = await _bed.CreateScheduleRunAsync(ScheduleRunDraft(second, Start.AddHours(3), "Second"));
        await _bed.WithScheduleRunCommandsAsync(commands => commands.Delete(deleted));

        async Task<IReadOnlyList<Guid>> Ids(Option<Guid> schedule, Option<ScheduleRunStatus> status, DeletionState state = DeletionState.Active) =>
            (await _bed.WithScheduleRunQueriesAsync(queries => queries.List(new Flipper(1, 10), schedule, status, state)))
                .Results.Match(() => [], items => items.Select(item => item.Id.Key).ToList());

        Assert.Equal([other, failed, old], await Ids(None, None));
        Assert.Equal([failed, old], await Ids(Some(first), None));
        Assert.Equal([failed], await Ids(None, Some(ScheduleRunStatus.Failed)));
        Assert.Equal([deleted], await Ids(None, None, DeletionState.Deleted));
        Assert.True((await _bed.WithScheduleRunQueriesAsync(queries => queries.Find(deleted, DeletionState.Active))).IsNone());
    }
}

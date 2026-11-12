using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using static Behind.Tests.Helpers.MessageTestBedExtensions;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class MessageEndpointsTests
{
    private static readonly Guid Id = Guid.Parse("18fdcc69-269d-4c1a-9123-56f83cd36d50");
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static IMessageQueries.MessageDetails Details() =>
        new(new VersionedKey<Guid>(Id, 2), "maintenance", "Notice", "<p>Message</p>", MessageLevel.Warning, Start, Start.AddHours(1));

    private static MessageEndpoints.SaveMessageRequest Request(
        string name = " Notice ", string? title = "", string? content = "<p>Safe</p>", string level = "info",
        DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null) =>
        new(name, title, content, level, startsAt, endsAt);

    [Fact]
    public async Task Live_UsesTheClockAndReturnsThePublicVersionWithoutCaching()
    {
        var queries = Substitute.For<IMessageQueries>();
        queries.FindLive(Id, Start, Arg.Any<CancellationToken>()).Returns(Some(Details()));
        var http = new DefaultHttpContext();

        var result = await MessageEndpoints.Live(Id, http, queries, new FixedTimeProvider(Start));

        var body = Assert.IsType<JsonHttpResult<MessageWidgetResponse>>(result).Value!;
        Assert.Equal((Id, 2L, "warning", "<p>Message</p>"), (body.Id, body.Version, body.Level, body.Content));
        Assert.Equal(ManagedListExtensions.NoStore, http.Response.Headers.CacheControl);
        await queries.Received(1).FindLive(Id, Start, http.RequestAborted);
    }

    [Fact]
    public async Task Live_NotLiveOrMissing_Returns404()
    {
        var queries = Substitute.For<IMessageQueries>();
        queries.FindLive(default, default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await MessageEndpoints.Live(Id, new DefaultHttpContext(), queries, new FixedTimeProvider(Start)));
    }

    [Fact]
    public async Task Live_SanitizesContentEvenWhenItWasStoredOutsideTheMessageEditor()
    {
        var queries = Substitute.For<IMessageQueries>();
        queries.FindLive(default, default, default).ReturnsForAnyArgs(Some(Details() with
        {
            Content = "<p onclick=\"alert(1)\">Safe</p><script>bad()</script>"
        }));

        var result = await MessageEndpoints.Live(Id, new DefaultHttpContext(), queries, new FixedTimeProvider(Start));
        var content = Assert.IsType<JsonHttpResult<MessageWidgetResponse>>(result).Value!.Content;

        Assert.Contains("<p>Safe</p>", content);
        Assert.DoesNotContain("onclick", content);
        Assert.DoesNotContain("<script", content);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    public async Task Live_EnforcesThePersistedScheduleAtItsBoundaries(int minutes, bool visible)
    {
        using var bed = new NewsTestBed();
        var key = await bed.CreateMessageAsync(MessageDraft(startsAt: Start, endsAt: Start.AddHours(1)));
        await using var context = bed.Read();
        var queries = new Behind.Core.Database.Queries.MessageQueries(context);

        var result = await MessageEndpoints.Live(key, new DefaultHttpContext(), queries, new FixedTimeProvider(Start.AddMinutes(minutes)));

        if (visible)
        {
            Assert.IsType<JsonHttpResult<MessageWidgetResponse>>(result);
        }
        else
        {
            Assert.IsType<NotFound>(result);
        }
    }

    [Fact]
    public async Task Live_DeletedMessage_Returns404()
    {
        using var bed = new NewsTestBed();
        var key = await bed.CreateMessageAsync();
        await bed.WithMessageCommandsAsync(commands => commands.Delete(key));
        await using var context = bed.Read();

        Assert.IsType<NotFound>(await MessageEndpoints.Live(key, new DefaultHttpContext(),
            new Behind.Core.Database.Queries.MessageQueries(context), new FixedTimeProvider(Start)));
    }

    [Theory]
    [InlineData("", "info")]
    [InlineData("Name", "fatal")]
    public async Task Create_InvalidFields_Returns400WithoutWriting(string name, string level)
    {
        var commands = Substitute.For<IMessageCommands>();

        Assert.IsType<BadRequest>(await MessageEndpoints.Create(Request(name: name, level: level), new DefaultHttpContext(), commands));
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_InvalidSchedule_Returns400(int hours)
    {
        var commands = Substitute.For<IMessageCommands>();

        Assert.IsType<BadRequest>(await MessageEndpoints.Create(
            Request(startsAt: Start, endsAt: Start.AddHours(hours)), new DefaultHttpContext(), commands));
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task Create_SanitizesContentAndAllowsAnEmptyTitle()
    {
        var commands = Substitute.For<IMessageCommands>();
        commands.Create(default!, default).ReturnsForAnyArgs(new VersionedSaveResult.Saved(new VersionedKey<Guid>(Id, 1)));

        var result = await MessageEndpoints.Create(Request(content: "<p onclick=\"alert(1)\">Safe</p><script>bad()</script>"),
            new DefaultHttpContext(), commands);

        Assert.Equal(new VersionedSavedResponse(Id, 1), Assert.IsType<JsonHttpResult<VersionedSavedResponse>>(result).Value);
        await commands.Received(1).Create(Arg.Is<IMessageCommands.MessageDraft>(draft =>
            draft.Name == "Notice" && draft.Title == "" && draft.Level == MessageLevel.Info
            && draft.Content.Contains("<p>Safe</p>") && !draft.Content.Contains("onclick") && !draft.Content.Contains("<script")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_DeletedMessage_RequiresTheDeletedRecordsGrant()
    {
        var queries = Substitute.For<IMessageQueries>();
        queries.Find(Id, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(None);
        queries.Find(Id, DeletionState.Deleted, Arg.Any<CancellationToken>()).Returns(Some(Details()));
        Assert.IsType<NotFound>(await MessageEndpoints.Get(Id, new DefaultHttpContext(), queries, new FakeAuthorizationService()));

        var result = await MessageEndpoints.Get(Id, new DefaultHttpContext(), queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant));

        Assert.True(Assert.IsType<JsonHttpResult<MessageDetailsResponse>>(result).Value!.IsDeleted);
    }

    [Fact]
    public async Task Manage_DeletedListing_RequiresTheDeletedRecordsGrant()
    {
        var result = await MessageEndpoints.Manage(new DefaultHttpContext(), Substitute.For<IMessageQueries>(),
            new FakeAuthorizationService(), deleted: true);

        Assert.Equal(403, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Changes_ForwardTheKeyAndMapTheResults()
    {
        var commands = Substitute.For<IMessageCommands>();
        commands.Update(Id, Arg.Any<IMessageCommands.MessageDraft>(), Arg.Any<CancellationToken>())
            .Returns(new VersionedSaveResult.Saved(new VersionedKey<Guid>(Id, 3)));
        commands.Delete(Id, Arg.Any<CancellationToken>()).Returns(true);
        commands.Restore(Id, Arg.Any<CancellationToken>()).Returns(true);
        commands.HardDelete(Id, Arg.Any<CancellationToken>()).Returns(HardDeleteResult.Deleted);
        var http = new DefaultHttpContext();

        Assert.Equal(new VersionedSavedResponse(Id, 3),
            Assert.IsType<JsonHttpResult<VersionedSavedResponse>>(await MessageEndpoints.Update(Id, Request(), http, commands)).Value);
        Assert.IsType<NoContent>(await MessageEndpoints.Delete(Id, http, commands));
        Assert.IsType<NoContent>(await MessageEndpoints.Restore(Id, http, commands));
        Assert.IsType<NoContent>(await MessageEndpoints.HardDelete(Id, http, commands));
    }
}

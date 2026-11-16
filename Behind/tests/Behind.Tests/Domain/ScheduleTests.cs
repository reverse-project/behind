using System.Text.Json;

namespace Behind.Tests.Domain;

public sealed class ScheduleTests
{
    private static readonly Guid RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(ScheduleNotification.Never, false, false)]
    [InlineData(ScheduleNotification.Always, true, true)]
    [InlineData(ScheduleNotification.Success, true, false)]
    [InlineData(ScheduleNotification.Failure, false, true)]
    public void Wants_FollowsTheNotificationSetting(ScheduleNotification notify, bool onSuccess, bool onFailure)
    {
        Assert.Equal((onSuccess, onFailure), (notify.Wants(true), notify.Wants(false)));
    }

    [Fact]
    public void Keys_AreCamelCaseAndParseCaseInsensitively()
    {
        Assert.Equal("notRequested", ScheduleRunEmail.NotRequested.Key);
        Assert.Equal("succeeded", ScheduleRunStatus.Succeeded.Key);
        Assert.Equal(Some(ScheduleNotification.Failure), " FAILURE ".ToScheduleNotification());
        Assert.Equal(Some(ScheduleParticipantKind.Email), "email".ToScheduleParticipantKind());
        Assert.Equal(Some(ScheduleRunEmail.NoRecipients), "noRecipients".ToScheduleRunEmail());
        Assert.True("unknown".ToScheduleRunStatus().IsNone());
        Assert.True(((string?)null).ToScheduleNotification().IsNone());
    }

    [Theory]
    [InlineData(ScheduleParticipantKind.Role, "11111111-1111-1111-1111-111111111111", true)]
    [InlineData(ScheduleParticipantKind.User, "00000000-0000-0000-0000-000000000000", false)]
    [InlineData(ScheduleParticipantKind.User, "nope", false)]
    [InlineData(ScheduleParticipantKind.Email, "ops@example.test", true)]
    [InlineData(ScheduleParticipantKind.Email, "Ops <ops@example.test>", false)]
    [InlineData(ScheduleParticipantKind.Email, "not-an-email", false)]
    [InlineData(ScheduleParticipantKind.Email, "", false)]
    public void Participant_IsValid_ChecksTheValueForTheKind(ScheduleParticipantKind kind, string value, bool valid)
    {
        Assert.Equal(valid, new ScheduleParticipant(kind, value).IsValid);
    }

    [Fact]
    public void Participants_NormalizeDeduplicateAndSplitByKind()
    {
        var participants = new ScheduleParticipants(
        [
            new(ScheduleParticipantKind.Role, RoleId.ToString("N")),
            new(ScheduleParticipantKind.Role, RoleId.ToString()),
            new(ScheduleParticipantKind.User, UserId.ToString().ToUpperInvariant()),
            new(ScheduleParticipantKind.Email, " ops@example.test "),
            new(ScheduleParticipantKind.Email, "OPS@example.test")
        ]).Normalized;

        Assert.Equal(3, participants.Items.Count);
        Assert.Equal([RoleId], participants.RoleIds);
        Assert.Equal([UserId], participants.UserIds);
        Assert.Equal(["ops@example.test"], participants.Emails);
        Assert.True(participants.IsValid);
        Assert.False(participants.IsEmpty);
        Assert.True(ScheduleParticipants.Empty.IsEmpty);
    }

    [Fact]
    public void Participants_JsonRoundTripsAndUsesValueEquality()
    {
        var participants = new ScheduleParticipants(
        [
            new(ScheduleParticipantKind.Role, RoleId.ToString()),
            new(ScheduleParticipantKind.Email, "ops@example.test")
        ]);

        var json = participants.ToJson();

        Assert.Contains("\"kind\":\"role\"", json);
        Assert.Equal(participants, ScheduleExtensions.ParseScheduleParticipants(json));
        Assert.Equal(participants, JsonSerializer.Deserialize<ScheduleParticipants>(JsonSerializer.Serialize(participants)));
        Assert.Equal(ScheduleParticipants.Empty, ScheduleExtensions.ParseScheduleParticipants(null));
        Assert.Throws<FormatException>(() => ScheduleExtensions.ParseScheduleParticipants("[{\"kind\":\"robot\",\"value\":\"x\"}]"));
    }

    [Fact]
    public void Parameters_JsonIsSortedAndRoundTrips()
    {
        var parameters = new ScheduleParameters(new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });

        Assert.Equal("{\"a\":\"1\",\"b\":\"2\"}", parameters.ToJson());
        Assert.Equal(parameters, ScheduleExtensions.ParseScheduleParameters(parameters.ToJson()));
        Assert.Equal(parameters, new ScheduleParameters(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }));
        Assert.Equal(ScheduleParameters.Empty, ScheduleExtensions.ParseScheduleParameters(""));
    }

    [Fact]
    public void NextRunAfter_IsNoneWhenDisabled()
    {
        var cron = Behind.Common.Scheduling.CronExpression.TryParse("0 * * * *").Match(() => throw new InvalidOperationException(), c => c);
        var moment = new DateTimeOffset(2026, 11, 16, 10, 30, 0, TimeSpan.Zero);
        var schedule = new Schedule("Hourly", cron, "test", ScheduleParameters.Empty, true, ScheduleNotification.Never, ScheduleParticipants.Empty);

        Assert.Equal(Some(moment.AddMinutes(30)), schedule.NextRunAfter(moment));
        Assert.True((schedule with { Enabled = false }).NextRunAfter(moment).IsNone());
    }
}

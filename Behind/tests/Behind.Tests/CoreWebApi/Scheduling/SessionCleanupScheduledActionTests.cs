using System.Security.Claims;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Scheduling;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Scheduling;

public sealed class SessionCleanupScheduledActionTests
{
    private static readonly DateTimeOffset ScheduledFor = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private readonly ISessionCommands _sessions = Substitute.For<ISessionCommands>();

    private SessionCleanupScheduledAction CreateAction(int maxAgeDays = SchedulerConfiguration.DefaultMaxSessionCleanupAgeDays)
    {
        var options = Substitute.For<IOptionsMonitor<SchedulerConfiguration>>();
        options.CurrentValue.Returns(new SchedulerConfiguration { MaxSessionCleanupAgeDays = maxAgeDays });
        return new SessionCleanupScheduledAction(_sessions, options);
    }

    private static Dictionary<string, string> Parameters(string? days) =>
        days is null ? [] : new() { [SessionCleanupScheduledAction.AgeParameter] = days };

    private static ScheduledActionInvocation Invocation(string? days) =>
        new(Guid.NewGuid(), "Nightly cleanup", ScheduledFor, Parameters(days));

    [Fact]
    public void Descriptor_ExposesAnOptionalNumericAgeDefaultingToThirtyDays()
    {
        var descriptor = CreateAction(90).Descriptor;

        Assert.Equal("core.session-cleanup", descriptor.Key);
        var parameter = Assert.Single(descriptor.Parameters);
        Assert.Equal(SessionCleanupScheduledAction.AgeParameter, parameter.Name);
        Assert.Equal(ScheduledActionParameterKind.Number, parameter.Kind);
        Assert.False(parameter.Required);
        Assert.Equal("30", parameter.DefaultValue);
        Assert.Equal(1, parameter.Minimum);
        Assert.Equal(90, parameter.Maximum);
    }

    [Fact]
    public async Task Options_ReturnsNothing()
    {
        Assert.Empty(await CreateAction().Options(new ClaimsPrincipal(), SessionCleanupScheduledAction.AgeParameter, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("30")]
    [InlineData(" 365 ")]
    [InlineData("3650")]
    public async Task Validate_AcceptsBlankOrWholeDaysWithinTheLimit(string? days)
    {
        Assert.Equal(None, await CreateAction().Validate(new ClaimsPrincipal(), Parameters(days), CancellationToken.None));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("3651")]
    [InlineData("99999999999")]
    public async Task Validate_RejectsInvalidAges(string days)
    {
        Assert.Equal(
            Some(SessionCleanupScheduledAction.InvalidAgeReason),
            await CreateAction().Validate(new ClaimsPrincipal(), Parameters(days), CancellationToken.None));
    }

    [Fact]
    public async Task Validate_HonoursTheConfiguredLimit()
    {
        Assert.Equal(
            Some(SessionCleanupScheduledAction.InvalidAgeReason),
            await CreateAction(10).Validate(new ClaimsPrincipal(), Parameters("11"), CancellationToken.None));
    }

    [Fact]
    public async Task Run_DeletesSessionsInvalidatedBeforeTheAgeCutoff()
    {
        _sessions.DeleteInvalidatedBefore(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(7);

        var result = await CreateAction().Run(Invocation("45"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Removed 7 invalidated session(s) older than 45 day(s).", result.Summary);
        Assert.Equal(None, result.Link);
        await _sessions.Received(1).DeleteInvalidatedBefore(ScheduledFor.AddDays(-45), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WithoutAge_UsesTheThirtyDayDefault()
    {
        var result = await CreateAction().Run(Invocation(null), CancellationToken.None);

        Assert.True(result.Succeeded);
        await _sessions.Received(1).DeleteInvalidatedBefore(ScheduledFor.AddDays(-30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WithInvalidAge_FailsWithoutDeleting()
    {
        var result = await CreateAction().Run(Invocation("0"), CancellationToken.None);

        Assert.False(result.Succeeded);
        await _sessions.DidNotReceiveWithAnyArgs().DeleteInvalidatedBefore(default, default);
    }
}

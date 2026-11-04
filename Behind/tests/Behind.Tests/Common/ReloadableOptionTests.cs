using Behind.Common.Configuration;
using Behind.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Tests.Common;

public class ReloadableOptionTests
{
    private sealed record Sample(string Name);

    private static ReloadableMemoryConfiguration ConfigurationWith(string name) =>
        new(new Dictionary<string, string?> { ["Sample:Name"] = name });

    private static Sample Bind(IConfiguration configuration) =>
        configuration.GetSection("Sample").GetValue<string>("Name") is { Length: > 0 } name
            ? new Sample(name)
            : throw new InvalidOperationException("Name is required");

    [Fact]
    public void Value_StartsWithTheBoundConfiguration()
    {
        using var option = new ReloadableOption<Sample>(ConfigurationWith("first"), Bind);

        Assert.Equal(new Sample("first"), option.Value);
    }

    [Fact]
    public void Value_AfterTheConfigurationChanges_ReturnsTheNewValueThroughEveryOptionsInterface()
    {
        var configuration = ConfigurationWith("first");
        using var option = new ReloadableOption<Sample>(configuration, Bind);
        IOptions<Sample> options = option;
        IOptionsSnapshot<Sample> snapshot = option;
        IOptionsMonitor<Sample> monitor = option;

        configuration.Update("Sample:Name", "second");

        Assert.Equal(new Sample("second"), options.Value);
        Assert.Equal(new Sample("second"), snapshot.Get("any"));
        Assert.Equal(new Sample("second"), monitor.CurrentValue);
    }

    [Fact]
    public void OnChange_NotifiesOnlyWhenTheBoundValueChanged()
    {
        var configuration = ConfigurationWith("first");
        using var option = new ReloadableOption<Sample>(configuration, Bind);
        var seen = new List<Sample>();
        using var subscription = option.OnChange((value, _) => seen.Add(value));

        configuration.Update("Other:Key", "unrelated");
        configuration.Update("Sample:Name", "second");

        Assert.Equal([new Sample("second")], seen);
    }

    [Fact]
    public void OnChange_AfterTheSubscriptionIsDisposed_DoesNotNotify()
    {
        var configuration = ConfigurationWith("first");
        using var option = new ReloadableOption<Sample>(configuration, Bind);
        var calls = 0;
        option.OnChange((_, _) => calls++).Dispose();

        configuration.Update("Sample:Name", "second");

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Value_WhenTheNewConfigurationCannotBeBound_KeepsThePreviousValueAndReportsTheFailure()
    {
        var configuration = ConfigurationWith("first");
        var failures = new List<Exception>();
        using var option = new ReloadableOption<Sample>(configuration, Bind, failures.Add);

        configuration.Update("Sample:Name", string.Empty);

        Assert.Equal(new Sample("first"), option.Value);
        Assert.IsType<InvalidOperationException>(Assert.Single(failures));
    }

    [Fact]
    public void Dispose_StopsFollowingTheConfiguration()
    {
        var configuration = ConfigurationWith("first");
        var option = new ReloadableOption<Sample>(configuration, Bind);
        option.Dispose();

        configuration.Update("Sample:Name", "second");

        Assert.Equal(new Sample("first"), option.Value);
    }

    [Fact]
    public void AddReloadableOptions_ResolvesTheSameLiveInstanceForEveryOptionsInterface()
    {
        var configuration = ConfigurationWith("first");
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddReloadableOptions(Bind)
            .BuildServiceProvider();

        configuration.Update("Sample:Name", "second");

        Assert.Equal(new Sample("second"), provider.GetRequiredService<IOptions<Sample>>().Value);
        Assert.Equal(new Sample("second"), provider.GetRequiredService<IOptionsSnapshot<Sample>>().Value);
        Assert.Equal(new Sample("second"), provider.GetRequiredService<IOptionsMonitor<Sample>>().CurrentValue);
    }

    [Fact]
    public void AddReloadableOptions_ReportsBindFailuresToTheSharedFailures()
    {
        var configuration = ConfigurationWith("first");
        var failures = new ConfigurationReloadFailures();
        var reported = new List<string>();
        failures.Failed += (source, _) => reported.Add(source);
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(failures)
            .AddReloadableOptions(Bind)
            .BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<Sample>>();

        configuration.Update("Sample:Name", string.Empty);

        Assert.Equal([nameof(Sample)], reported);
    }
}

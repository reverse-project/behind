using Behind.Common.Configuration;
using Behind.Common.Logging;
using Behind.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Behind.Tests.Common;

public sealed class ResilientJsonConfigurationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    private string Write(string name, string json)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, json);

        return path;
    }

    private HostApplicationBuilder BuilderFor(string path, bool optional = false)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = _directory });
        builder.AddReloadableJsonFile(path, optional);

        return builder;
    }

    [Fact]
    public async Task AddReloadableJsonFile_PicksUpChangesOfTheFile()
    {
        var path = Write("app.json", """{ "Section": { "Value": "one" } }""");
        var builder = BuilderFor(path);

        File.WriteAllText(path, """{ "Section": { "Value": "two" } }""");

        Assert.Equal("two", await Wait.UntilAsync(() => builder.Configuration["Section:Value"], value => value == "two"));
    }

    [Fact]
    public async Task AddReloadableJsonFile_OptionalFileCreatedLater_IsPickedUp()
    {
        var path = Path.Combine(_directory, "later.json");
        var builder = BuilderFor(path, optional: true);

        File.WriteAllText(path, """{ "Section": { "Value": "created" } }""");

        Assert.Equal("created", await Wait.UntilAsync(() => builder.Configuration["Section:Value"], value => value == "created"));
    }

    [Fact]
    public async Task AddReloadableJsonFile_InvalidJsonAfterTheFirstLoad_KeepsTheLastGoodValuesAndReportsTheFailure()
    {
        var path = Write("app.json", """{ "Section": { "Value": "good" } }""");
        var builder = BuilderFor(path);
        var failures = new List<string>();
        var shared = (ConfigurationReloadFailures)builder.Services
            .Single(d => d.ServiceType == typeof(ConfigurationReloadFailures)).ImplementationInstance!;
        shared.Failed += (source, _) => failures.Add(source);

        File.WriteAllText(path, "{ not json");
        await Wait.UntilAsync(() => failures.Count, count => count > 0);

        Assert.Equal("good", builder.Configuration["Section:Value"]);
        Assert.Equal(Path.GetFileName(path), failures.First());
    }

    [Fact]
    public async Task AddReloadableJsonFile_FixedFileAfterInvalidJson_LoadsAgain()
    {
        var path = Write("app.json", """{ "Section": { "Value": "good" } }""");
        var builder = BuilderFor(path);

        File.WriteAllText(path, "{ not json");
        await Task.Delay(500);
        File.WriteAllText(path, """{ "Section": { "Value": "fixed" } }""");

        Assert.Equal("fixed", await Wait.UntilAsync(() => builder.Configuration["Section:Value"], value => value == "fixed"));
    }

    [Fact]
    public void AddReloadableJsonFile_InvalidJsonOnTheFirstLoad_Throws()
    {
        var path = Write("app.json", "{ not json");

        Assert.ThrowsAny<Exception>(() => BuilderFor(path));
    }

    [Fact]
    public void AddReloadableJsonFile_AfterAddSerilog_KeepsTheStreamBasedDefaultsWorking()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = _directory });
        builder.AddSerilog();

        builder.AddReloadableJsonFile(Write("app.json", """{ "Section": { "Value": "one" } }"""), false);

        Assert.Equal("one", builder.Configuration["Section:Value"]);
        Assert.Equal("Information", builder.Configuration["Serilog:MinimumLevel:Default"]);
    }

    [Fact]
    public void AddReloadableJsonFile_RegistersTheFailureLoggerOnce()
    {
        var builder = BuilderFor(Write("one.json", "{}"));
        builder.AddReloadableJsonFile(Write("two.json", "{}"), false);

        Assert.Single(builder.Services, d => d.ImplementationType == typeof(ConfigurationReloadFailureLogger));
        Assert.Single(builder.Services, d => d.ServiceType == typeof(ConfigurationReloadFailures));
    }

    [Fact]
    public async Task ConfigurationReloadFailureLogger_LogsReportedFailures()
    {
        var failures = new ConfigurationReloadFailures();
        var logger = new CapturingLogger<ConfigurationReloadFailureLogger>();
        var service = new ConfigurationReloadFailureLogger(failures, logger);

        await service.StartAsync(CancellationToken.None);
        failures.Report("email.json", new InvalidOperationException("bad"));
        await service.StopAsync(CancellationToken.None);
        failures.Report("email.json", new InvalidOperationException("ignored"));

        Assert.Equal(1, logger.Entries.Count(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error));
    }

    [Fact]
    public async Task OptionsCacheInvalidationService_ClearsTheTargetCacheWhenTheSourceChanges()
    {
        var configuration = new ReloadableMemoryConfiguration(new Dictionary<string, string?> { ["Sample:Name"] = "first" });
        using var source = new ReloadableOption<string>(configuration, c => c["Sample:Name"]!);
        var cache = new OptionsCache<object>();
        cache.GetOrAdd("name", () => new object());
        var service = new OptionsCacheInvalidationService<string, object>(source, cache);

        await service.StartAsync(CancellationToken.None);
        configuration.Update("Sample:Name", "second");
        await service.StopAsync(CancellationToken.None);

        Assert.True(cache.TryAdd("name", new object()));
    }
}
